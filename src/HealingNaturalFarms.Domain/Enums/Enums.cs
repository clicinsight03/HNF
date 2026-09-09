namespace HealingNaturalFarms.Domain.Enums;

/// <summary>
/// The two storefronts this system serves. Each region has its own catalog
/// availability, pricing/currency, and set of payment providers.
/// </summary>
public enum RegionCode
{
    US,
    IN
}

public enum CurrencyCode
{
    USD,
    INR
}

/// <summary>
/// The two "departments" of the store. Kept as a flat enum rather than a
/// pure category tree so that type-specific attributes (below) can be
/// validated/rendered without walking a category hierarchy.
/// </summary>
public enum ProductType
{
    Plant,
    Produce
}

public enum UnitOfSale
{
    PerItem,
    PerLb,
    PerKg,
    PerBunch,
    PerDozen
}

public enum SunlightNeeds
{
    FullSun,
    PartialSun,
    Shade
}

public enum PaymentProvider
{
    Stripe,
    PayPal,
    Razorpay
}

public enum PaymentStatus
{
    Pending,
    RequiresAction,
    Succeeded,
    Failed,
    Refunded,
    PartiallyRefunded
}

public enum OrderStatus
{
    PendingPayment,
    Paid,
    Processing,
    Shipped,
    Delivered,
    Cancelled,
    Refunded
}

public enum DevicePlatform
{
    Android,
    iOS
}
