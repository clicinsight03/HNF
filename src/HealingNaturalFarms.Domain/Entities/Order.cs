using HealingNaturalFarms.Domain.Enums;

namespace HealingNaturalFarms.Domain.Entities;

public class Order
{
    public int Id { get; set; }
    public required string OrderNumber { get; set; } // human-facing, e.g. HNF-US-000123
    public int? UserId { get; set; } // see the note on Address.UserId re: no User navigation here
    public RegionCode Region { get; set; }
    public CurrencyCode Currency { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;

    public int ShippingAddressId { get; set; }
    public Address? ShippingAddress { get; set; }

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }

    // Snapshot fields so historical orders remain accurate even if the
    // product is later renamed, re-priced, or removed from the catalog.
    public required string ProductNameSnapshot { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

/// <summary>
/// One attempted or completed payment against an order. An order can have
/// more than one row here (e.g. a failed Stripe attempt followed by a
/// successful retry), so Payments is a collection rather than a
/// one-to-one with Order.
/// </summary>
public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public PaymentProvider Provider { get; set; }

    /// <summary>Provider-side identifier: Stripe PaymentIntent id, PayPal
    /// order id, or Razorpay payment id.</summary>
    public required string ProviderReference { get; set; }
    public decimal Amount { get; set; }
    public CurrencyCode Currency { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Raw JSON of the provider's create/confirm/webhook response,
    /// kept for support/dispute investigation.</summary>
    public string? RawResponseJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
