using HealingNaturalFarms.Domain.Dtos;
using HealingNaturalFarms.Domain.Entities;
using HealingNaturalFarms.Domain.Enums;
using HealingNaturalFarms.Infrastructure.Data;
using HealingNaturalFarms.Payments.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HealingNaturalFarms.Api.Services;

public interface ICheckoutService
{
    Task<CreateCheckoutResponse> CreateCheckoutAsync(int? userId, CreateCheckoutRequest request, CancellationToken ct = default);
    Task<OrderDetailDto> ConfirmPaymentAsync(ConfirmPaymentRequest request, CancellationToken ct = default);
}

/// <summary>
/// Orchestrates checkout: re-validates the cart against live prices/stock
/// (never trusts client-supplied totals), creates the Order/OrderItems
/// rows, then hands off to whichever gateway the client chose via
/// IPaymentGatewayResolver. Tax and shipping are simple placeholders
/// (flat shipping, no tax) - swap ComputeTax/ComputeShipping for real
/// logic (a tax API, carrier rates) when that's ready.
/// </summary>
public class CheckoutService(AppDbContext db, IPaymentGatewayResolver gatewayResolver, IPushNotificationSender pushSender, IOrderEmailSender emailSender) : ICheckoutService
{
    private static readonly Random OrderNumberRandom = new();

    public async Task<CreateCheckoutResponse> CreateCheckoutAsync(int? userId, CreateCheckoutRequest request, CancellationToken ct = default)
    {
        var cart = await db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == request.CartId, ct)
            ?? throw new KeyNotFoundException($"Cart {request.CartId} not found.");

        if (cart.Items.Count == 0)
        {
            throw new InvalidOperationException("Cart is empty.");
        }

        var availableProviders = gatewayResolver.GetAvailableProviders(cart.Region);
        if (!availableProviders.Contains(request.Provider))
        {
            throw new InvalidOperationException($"{request.Provider} is not available for region {cart.Region}.");
        }

        // Re-validate every line against the live listing - the cart's
        // UnitPriceSnapshot is a display convenience, not the source of truth.
        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var listings = await db.ProductRegionListings
            .Where(l => l.Region == cart.Region && productIds.Contains(l.ProductId))
            .ToDictionaryAsync(l => l.ProductId, ct);
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var orderItems = new List<OrderItem>();
        decimal subtotal = 0m;

        foreach (var item in cart.Items)
        {
            if (!listings.TryGetValue(item.ProductId, out var listing) || !listing.IsAvailable)
            {
                throw new InvalidOperationException($"Product {item.ProductId} is no longer available in {cart.Region}.");
            }

            if (listing.StockQuantity < item.Quantity)
            {
                throw new InvalidOperationException($"Only {listing.StockQuantity} of product {item.ProductId} left in stock.");
            }

            var lineTotal = listing.Price * item.Quantity;
            subtotal += lineTotal;

            orderItems.Add(new OrderItem
            {
                ProductId = item.ProductId,
                ProductNameSnapshot = products[item.ProductId].Name,
                UnitPrice = listing.Price,
                Quantity = item.Quantity,
                LineTotal = lineTotal
            });
        }

        var currency = cart.Region == RegionCode.US ? CurrencyCode.USD : CurrencyCode.INR;
        var shipping = ComputeShipping(cart.Region, subtotal);
        var tax = ComputeTax(cart.Region, subtotal);
        var total = subtotal + shipping + tax;

        var address = new Address
        {
            UserId = userId ?? 0,
            FullName = request.ShippingAddress.FullName,
            Line1 = request.ShippingAddress.Line1,
            Line2 = request.ShippingAddress.Line2,
            City = request.ShippingAddress.City,
            State = request.ShippingAddress.State,
            PostalCode = request.ShippingAddress.PostalCode,
            Country = request.ShippingAddress.Country,
            Phone = request.ShippingAddress.Phone
        };
        db.Addresses.Add(address);
        await db.SaveChangesAsync(ct); // need address.Id before attaching to the order

        var order = new Order
        {
            OrderNumber = GenerateOrderNumber(cart.Region),
            UserId = userId,
            Region = cart.Region,
            Currency = currency,
            Subtotal = subtotal,
            Tax = tax,
            ShippingCost = shipping,
            Total = total,
            Status = OrderStatus.PendingPayment,
            ShippingAddressId = address.Id,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Items = orderItems
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        var gateway = gatewayResolver.Resolve(request.Provider);
        var intent = await gateway.CreatePaymentAsync(total, currency, order.OrderNumber, ct);

        db.Payments.Add(new Payment
        {
            OrderId = order.Id,
            Provider = request.Provider,
            ProviderReference = intent.ProviderReference,
            Amount = total,
            Currency = currency,
            Status = PaymentStatus.Pending,
            RawResponseJson = intent.RawResponseJson
        });
        await db.SaveChangesAsync(ct);

        return new CreateCheckoutResponse(
            order.Id, order.OrderNumber, request.Provider, total, currency,
            intent.ClientSecret, intent.ProviderOrderId, intent.ProviderPublicKey);
    }

    public async Task<OrderDetailDto> ConfirmPaymentAsync(ConfirmPaymentRequest request, CancellationToken ct = default)
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.Payments)
            .Include(o => o.ShippingAddress)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct)
            ?? throw new KeyNotFoundException($"Order {request.OrderId} not found.");

        var payment = order.Payments
            .Where(p => p.Provider == request.Provider)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No payment attempt found for this order/provider.");

        var gateway = gatewayResolver.Resolve(request.Provider);
        var verification = await gateway.VerifyAndCaptureAsync(
            new PaymentVerificationInput(payment.ProviderReference, request.ProviderPaymentReference, request.ProviderSignature), ct);

        payment.Status = verification.Status;
        payment.RawResponseJson = verification.RawResponseJson;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        if (verification.Success)
        {
            order.Status = OrderStatus.Paid;
            order.UpdatedAt = DateTimeOffset.UtcNow;

            // Decrement stock now that payment is confirmed.
            var productIds = order.Items.Select(i => i.ProductId).ToList();
            var listings = await db.ProductRegionListings
                .Where(l => l.Region == order.Region && productIds.Contains(l.ProductId))
                .ToListAsync(ct);
            foreach (var item in order.Items)
            {
                var listing = listings.FirstOrDefault(l => l.ProductId == item.ProductId);
                if (listing is not null)
                {
                    listing.StockQuantity = Math.Max(0, listing.StockQuantity - item.Quantity);
                }
            }
        }

        await db.SaveChangesAsync(ct);

        if (verification.Success)
        {
            if (order.UserId is not null)
            {
                await NotifyOrderPaidAsync(order.UserId.Value, order.OrderNumber, ct);
            }

            await EmailOrderConfirmationAsync(order, ct);
        }

        return new OrderDetailDto(
            order.Id, order.OrderNumber, order.Region, order.Currency,
            order.Subtotal, order.Tax, order.ShippingCost, order.Total, order.Status,
            new AddressDto(order.ShippingAddress!.Id, order.ShippingAddress.FullName, order.ShippingAddress.Line1,
                order.ShippingAddress.Line2, order.ShippingAddress.City, order.ShippingAddress.State,
                order.ShippingAddress.PostalCode, order.ShippingAddress.Country, order.ShippingAddress.Phone),
            order.Items.Select(i => new CartItemDto(i.ProductId, i.ProductNameSnapshot, null, i.UnitPrice, i.Quantity, i.LineTotal)).ToList(),
            order.CreatedAt);
    }

    /// <summary>Best-effort: a device send failure must never fail the
    /// checkout flow itself, since the order is already correctly paid at
    /// this point.</summary>
    private async Task NotifyOrderPaidAsync(int userId, string orderNumber, CancellationToken ct)
    {
        var tokens = await db.DeviceRegistrations
            .Where(d => d.UserId == userId)
            .Select(d => d.PushToken)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            try
            {
                await pushSender.SendAsync(token, "Order confirmed", $"Your order {orderNumber} is confirmed and being prepared.", null, ct);
            }
            catch
            {
                // Logged by the HttpClient/telemetry pipeline in a real
                // deployment; swallow here so one bad token doesn't block
                // the others or the checkout response.
            }
        }
    }

    /// <summary>Same best-effort contract as NotifyOrderPaidAsync above -
    /// the customer's payment already succeeded and the order is already
    /// saved as Paid, so a flaky mail server must never turn that into a
    /// failed checkout response. Guest orders (order.UserId is null) still
    /// get emailed, since ContactEmail is collected at checkout regardless
    /// of whether the shopper has an account.</summary>
    private async Task EmailOrderConfirmationAsync(Order order, CancellationToken ct)
    {
        try
        {
            await emailSender.SendOrderConfirmationAsync(order, ct);
        }
        catch
        {
            // Logged by the SMTP client's own diagnostics/telemetry in a
            // real deployment; swallow here for the same reason as the
            // push-notification loop above.
        }
    }

    private static decimal ComputeShipping(RegionCode region, decimal subtotal)
    {
        if (subtotal <= 0) return 0m;
        // Simple flat-rate placeholder, free over a threshold.
        return region switch
        {
            RegionCode.US => subtotal >= 75m ? 0m : 8.99m,
            RegionCode.IN => subtotal >= 1500m ? 0m : 99m,
            _ => 0m
        };
    }

    private static decimal ComputeTax(RegionCode region, decimal subtotal)
    {
        // Placeholder only - real sales tax (US, varies by state/locality)
        // and GST (India) need a proper tax service; wire one in here.
        return 0m;
    }

    private static string GenerateOrderNumber(RegionCode region)
    {
        var suffix = OrderNumberRandom.Next(100000, 999999);
        return $"HNF-{region}-{suffix}";
    }
}
