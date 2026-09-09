using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HealingNaturalFarms.Domain.Enums;
using HealingNaturalFarms.Payments.Abstractions;
using HealingNaturalFarms.Payments.Options;

namespace HealingNaturalFarms.Payments.Gateways;

/// <summary>
/// Talks to Stripe's REST API directly over HttpClient rather than the
/// Stripe.net SDK - Stripe's API is stable, well-documented, and this
/// keeps the project's dependency footprint to just the base class
/// library (no third-party payment SDK to track for breaking changes).
/// Uses the PaymentIntents API with automatic_payment_methods so the
/// client can use Stripe's Payment Element for cards, wallets, etc.
/// </summary>
public class StripeGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly StripeOptions _options;

    public PaymentProvider Provider => PaymentProvider.Stripe;

    public StripeGateway(HttpClient http, StripeOptions options)
    {
        _http = http;
        _http.BaseAddress ??= new Uri("https://api.stripe.com/v1/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.SecretKey}:")));
        _options = options;
    }

    public async Task<PaymentIntentResult> CreatePaymentAsync(decimal amount, CurrencyCode currency, string orderNumber, CancellationToken ct = default)
    {
        var minorUnits = ToMinorUnits(amount, currency);
        var form = new Dictionary<string, string>
        {
            ["amount"] = minorUnits.ToString(),
            ["currency"] = currency.ToString().ToLowerInvariant(),
            ["automatic_payment_methods[enabled]"] = "true",
            ["metadata[order_number]"] = orderNumber
        };

        using var response = await _http.PostAsync("payment_intents", new FormUrlEncodedContent(form), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentGatewayException(PaymentProvider.Stripe, $"Stripe payment_intents create failed ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var id = root.GetProperty("id").GetString()!;
        var clientSecret = root.GetProperty("client_secret").GetString();

        return new PaymentIntentResult(
            ProviderReference: id,
            ClientSecret: clientSecret,
            ProviderOrderId: null,
            ProviderPublicKey: _options.PublishableKey,
            RawResponseJson: body);
    }

    public async Task<PaymentVerificationResult> VerifyAndCaptureAsync(PaymentVerificationInput input, CancellationToken ct = default)
    {
        // Payment confirmation happens client-side via Stripe.js against the
        // client_secret; here we just re-fetch the PaymentIntent server-side
        // and trust Stripe's own status, not anything the client claims.
        using var response = await _http.GetAsync($"payment_intents/{input.ProviderReference}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return new PaymentVerificationResult(false, PaymentStatus.Failed, $"Stripe lookup failed: {body}", body);
        }

        using var doc = JsonDocument.Parse(body);
        var status = doc.RootElement.GetProperty("status").GetString();

        return status switch
        {
            "succeeded" => new PaymentVerificationResult(true, PaymentStatus.Succeeded, null, body),
            "processing" or "requires_capture" => new PaymentVerificationResult(false, PaymentStatus.Pending, "Payment still processing", body),
            "requires_action" or "requires_confirmation" or "requires_payment_method" => new PaymentVerificationResult(false, PaymentStatus.RequiresAction, status, body),
            _ => new PaymentVerificationResult(false, PaymentStatus.Failed, status, body)
        };
    }

    /// <summary>Stripe (and Razorpay) want amounts in the smallest currency
    /// unit - cents for USD, paise for INR - both of which happen to be
    /// amount * 100 for the two currencies this store supports.</summary>
    private static long ToMinorUnits(decimal amount, CurrencyCode currency) => currency switch
    {
        CurrencyCode.USD or CurrencyCode.INR => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero),
        _ => throw new NotSupportedException($"Unsupported currency for Stripe: {currency}")
    };
}

public class PaymentGatewayException(PaymentProvider provider, string message) : Exception(message)
{
    public PaymentProvider Provider { get; } = provider;
}
