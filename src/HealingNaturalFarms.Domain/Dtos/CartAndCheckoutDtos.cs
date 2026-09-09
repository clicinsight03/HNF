using HealingNaturalFarms.Domain.Enums;

namespace HealingNaturalFarms.Domain.Dtos;

public record CartItemDto(
    int ProductId,
    string Name,
    string? ThumbnailUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

public record CartDto(
    int CartId,
    RegionCode Region,
    CurrencyCode Currency,
    IReadOnlyList<CartItemDto> Items,
    decimal Subtotal);

public record AddCartItemRequest(int ProductId, int Quantity);
public record UpdateCartItemRequest(int Quantity);

public record AddressDto(
    int? Id,
    string FullName,
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string Country,
    string? Phone);

/// <summary>
/// Kicks off checkout: the client picks which of the region's available
/// providers to pay with (US -> Stripe or PayPal; India -> Razorpay).
/// The server re-validates cart prices/stock before creating the
/// provider-side payment object - it never trusts client-supplied totals.
/// </summary>
public record CreateCheckoutRequest(
    int CartId,
    PaymentProvider Provider,
    AddressDto ShippingAddress,
    string ContactEmail,
    string? ContactPhone);

/// <summary>
/// What the client needs to complete payment client-side:
/// - Stripe: ClientSecret for confirmCardPayment / Payment Element.
/// - PayPal: PayPal order id (ProviderOrderId) to pass to their JS/SDK Buttons.
/// - Razorpay: OrderId + the publishable key id to open Razorpay Checkout.
/// </summary>
public record CreateCheckoutResponse(
    int OrderId,
    string OrderNumber,
    PaymentProvider Provider,
    decimal Amount,
    CurrencyCode Currency,
    string? ClientSecret,
    string? ProviderOrderId,
    string? ProviderPublicKey);

public record ConfirmPaymentRequest(int OrderId, PaymentProvider Provider, string ProviderPaymentReference, string? ProviderSignature);

public record OrderSummaryDto(
    int Id,
    string OrderNumber,
    RegionCode Region,
    CurrencyCode Currency,
    decimal Total,
    OrderStatus Status,
    DateTimeOffset CreatedAt);

public record OrderDetailDto(
    int Id,
    string OrderNumber,
    RegionCode Region,
    CurrencyCode Currency,
    decimal Subtotal,
    decimal Tax,
    decimal ShippingCost,
    decimal Total,
    OrderStatus Status,
    AddressDto ShippingAddress,
    IReadOnlyList<CartItemDto> Items,
    DateTimeOffset CreatedAt);
