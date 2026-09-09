using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HealingNaturalFarms.Domain.Enums;
using HealingNaturalFarms.Payments.Abstractions;
using HealingNaturalFarms.Payments.Options;

namespace HealingNaturalFarms.Payments.Gateways;

/// <summary>
/// Razorpay Orders API for the India storefront, called directly over
/// HttpClient (no Razorpay SDK dependency, same rationale as the other
/// two gateways). Order creation happens server-side; the client opens
/// Razorpay Checkout with the returned order id + the public key id,
/// and the resulting payment is verified server-side via HMAC-SHA256
/// signature check before the order is marked paid - Razorpay's
/// documented and required verification step, since a client could
/// otherwise claim success for a payment that never happened.
/// </summary>
public class RazorpayGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly RazorpayOptions _options;

    public PaymentProvider Provider => PaymentProvider.Razorpay;

    public RazorpayGateway(HttpClient http, RazorpayOptions options)
    {
        _http = http;
        _options = options;
        _http.BaseAddress ??= new Uri("https://api.razorpay.com/v1/");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.KeyId}:{options.KeySecret}")));
    }

    public async Task<PaymentIntentResult> CreatePaymentAsync(decimal amount, CurrencyCode currency, string orderNumber, CancellationToken ct = default)
    {
        if (currency != CurrencyCode.INR)
        {
            throw new NotSupportedException("Razorpay is configured for INR only in this store.");
        }

        var payload = new
        {
            amount = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero), // paise
            currency = "INR",
            receipt = orderNumber,
            payment_capture = 1 // auto-capture on successful payment
        };

        using var response = await _http.PostAsync("orders",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentGatewayException(PaymentProvider.Razorpay, $"Razorpay order create failed ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.GetProperty("id").GetString()!;

        return new PaymentIntentResult(
            ProviderReference: id,
            ClientSecret: null,
            ProviderOrderId: id,
            ProviderPublicKey: _options.KeyId,
            RawResponseJson: body);
    }

    public Task<PaymentVerificationResult> VerifyAndCaptureAsync(PaymentVerificationInput input, CancellationToken ct = default)
    {
        // Razorpay Checkout returns razorpay_order_id, razorpay_payment_id
        // and razorpay_signature to the client, which forwards them here.
        // The signature is HMAC-SHA256(order_id + "|" + payment_id, key_secret).
        if (string.IsNullOrEmpty(input.ProviderPaymentReference) || string.IsNullOrEmpty(input.ProviderSignature))
        {
            return Task.FromResult(new PaymentVerificationResult(false, PaymentStatus.Failed, "Missing payment id or signature", string.Empty));
        }

        var payload = $"{input.ProviderReference}|{input.ProviderPaymentReference}";
        var expectedSignature = ComputeHmacSha256Hex(payload, _options.KeySecret);

        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(input.ProviderSignature));

        var result = isValid
            ? new PaymentVerificationResult(true, PaymentStatus.Succeeded, null, payload)
            : new PaymentVerificationResult(false, PaymentStatus.Failed, "Signature mismatch", payload);

        return Task.FromResult(result);
    }

    private static string ComputeHmacSha256Hex(string message, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexStringLower(hash);
    }
}
