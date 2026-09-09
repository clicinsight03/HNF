using HealingNaturalFarms.Domain.Enums;

namespace HealingNaturalFarms.Payments.Abstractions;

/// <summary>
/// Everything the caller needs to hand off to the client SDK to complete
/// payment: Stripe wants a client secret for its Payment Element /
/// confirmCardPayment call; PayPal and Razorpay want a provider-side
/// order id (and, for Razorpay, the public key id) to open their own
/// checkout widget.
/// </summary>
public record PaymentIntentResult(
    string ProviderReference,
    string? ClientSecret,
    string? ProviderOrderId,
    string? ProviderPublicKey,
    string RawResponseJson);

public record PaymentVerificationInput(
    string ProviderReference,
    string? ProviderPaymentReference,
    string? ProviderSignature);

public record PaymentVerificationResult(bool Success, PaymentStatus Status, string? FailureReason, string RawResponseJson);

/// <summary>
/// One implementation per provider (Stripe, PayPal, Razorpay). Resolved by
/// <see cref="IPaymentGatewayResolver"/> based on the region's allowed
/// providers and the client's chosen provider - the storefront never
/// hard-codes "US therefore Stripe"; it's a configurable mapping (see
/// PaymentRegionOptions) precisely because a merchant may want to offer
/// PayPal as an alternative in the US, or add Stripe in India later.
/// </summary>
public interface IPaymentGateway
{
    PaymentProvider Provider { get; }

    /// <summary>Creates the provider-side payment/order object for the given
    /// amount. Amount must already be in the smallest sane unit for the
    /// currency at the *decimal* level (e.g. 19.99) - each gateway
    /// implementation handles converting to the provider's expected unit
    /// (Stripe/Razorpay want integer minor units; PayPal wants a decimal
    /// string).</summary>
    Task<PaymentIntentResult> CreatePaymentAsync(decimal amount, CurrencyCode currency, string orderNumber, CancellationToken ct = default);

    /// <summary>Verifies and (where the provider requires an explicit
    /// second step, e.g. PayPal capture) finalizes the payment. Also used
    /// to validate incoming webhooks/signatures before trusting them.</summary>
    Task<PaymentVerificationResult> VerifyAndCaptureAsync(PaymentVerificationInput input, CancellationToken ct = default);
}

public interface IPaymentGatewayResolver
{
    IPaymentGateway Resolve(PaymentProvider provider);
    IReadOnlyList<PaymentProvider> GetAvailableProviders(RegionCode region);
}
