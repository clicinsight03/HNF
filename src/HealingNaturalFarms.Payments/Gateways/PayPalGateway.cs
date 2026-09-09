using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HealingNaturalFarms.Domain.Enums;
using HealingNaturalFarms.Payments.Abstractions;
using HealingNaturalFarms.Payments.Options;

namespace HealingNaturalFarms.Payments.Gateways;

/// <summary>
/// PayPal REST API v2 (Orders) accessed directly over HttpClient. PayPal
/// uses OAuth2 client-credentials for server-to-server calls; the token
/// is cached in memory for its lifetime to avoid a round trip on every
/// checkout (PayPal tokens are typically valid ~9 hours).
/// </summary>
public class PayPalGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly PayPalOptions _options;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public PaymentProvider Provider => PaymentProvider.PayPal;

    public PayPalGateway(HttpClient http, PayPalOptions options)
    {
        _http = http;
        _options = options;
        _http.BaseAddress ??= new Uri(options.Environment.Equals("live", StringComparison.OrdinalIgnoreCase)
            ? "https://api-m.paypal.com/"
            : "https://api-m.sandbox.paypal.com/");
    }

    public async Task<PaymentIntentResult> CreatePaymentAsync(decimal amount, CurrencyCode currency, string orderNumber, CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);

        var payload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = orderNumber,
                    amount = new { currency_code = currency.ToString(), value = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/checkout/orders")
        {
            Content = JsonContent(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentGatewayException(PaymentProvider.PayPal, $"PayPal order create failed ({(int)response.StatusCode}): {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.GetProperty("id").GetString()!;

        return new PaymentIntentResult(
            ProviderReference: id,
            ClientSecret: null,
            ProviderOrderId: id,
            ProviderPublicKey: _options.ClientId,
            RawResponseJson: body);
    }

    public async Task<PaymentVerificationResult> VerifyAndCaptureAsync(PaymentVerificationInput input, CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"v2/checkout/orders/{input.ProviderReference}/capture");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            return new PaymentVerificationResult(false, PaymentStatus.Failed, $"PayPal capture failed: {body}", body);
        }

        using var doc = JsonDocument.Parse(body);
        var status = doc.RootElement.GetProperty("status").GetString();

        return status == "COMPLETED"
            ? new PaymentVerificationResult(true, PaymentStatus.Succeeded, null, body)
            : new PaymentVerificationResult(false, PaymentStatus.Failed, status, body);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
        {
            return _cachedToken;
        }

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            {
                return _cachedToken;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" })
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.ClientId}:{_options.ClientSecret}")));

            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(body);
            var token = doc.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

            _cachedToken = token;
            // Refresh a minute early to avoid using a token that expires
            // mid-request.
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);
            return token;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
}
