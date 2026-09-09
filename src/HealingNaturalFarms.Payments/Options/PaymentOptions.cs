using HealingNaturalFarms.Domain.Enums;

namespace HealingNaturalFarms.Payments.Options;

/// <summary>
/// Bind this whole section from configuration (appsettings.json / user
/// secrets / environment variables) via
/// builder.Configuration.GetSection("Payments").Get&lt;PaymentsOptions&gt;().
/// Real keys must never be committed - see the .env.example / secrets
/// notes in the Api project's README.
/// </summary>
public class PaymentsOptions
{
    public const string SectionName = "Payments";

    public StripeOptions Stripe { get; set; } = new();
    public PayPalOptions PayPal { get; set; } = new();
    public RazorpayOptions Razorpay { get; set; } = new();

    /// <summary>Which providers each region's checkout offers. Defaults
    /// to US -> Stripe + PayPal, India -> Razorpay, but is fully
    /// configurable (e.g. to add Stripe/PayPal as a backup option in
    /// India once that's wired up).</summary>
    public Dictionary<RegionCode, List<PaymentProvider>> RegionProviders { get; set; } = new()
    {
        [RegionCode.US] = [PaymentProvider.Stripe, PaymentProvider.PayPal],
        [RegionCode.IN] = [PaymentProvider.Razorpay]
    };
}

public class StripeOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public string WebhookSigningSecret { get; set; } = string.Empty;
}

public class PayPalOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    /// <summary>"sandbox" or "live" - selects the API base URL.</summary>
    public string Environment { get; set; } = "sandbox";
}

public class RazorpayOptions
{
    public string KeyId { get; set; } = string.Empty;
    public string KeySecret { get; set; } = string.Empty;
}
