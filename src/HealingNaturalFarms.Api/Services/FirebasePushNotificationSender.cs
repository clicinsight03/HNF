using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace HealingNaturalFarms.Api.Services;

public interface IPushNotificationSender
{
    Task SendAsync(string deviceToken, string title, string body, IDictionary<string, string>? data = null, CancellationToken ct = default);
}

/// <summary>
/// Sends pushes to both Android and iOS MAUI clients through a single
/// Firebase Cloud Messaging project (FCM v1 API) - FCM relays to APNs
/// for iOS devices under the hood, so one Firebase project covers both
/// platforms without a separate direct-APNs integration. Talks to the
/// FCM v1 REST API directly (no Firebase Admin SDK dependency): signs a
/// short-lived JWT with the service account's private key, exchanges it
/// for an OAuth2 access token, then POSTs the message.
///
/// Configure via the "Firebase" section in appsettings (ProjectId +
/// ServiceAccountJsonPath pointing at the JSON key file downloaded from
/// Firebase Console > Project Settings > Service Accounts). Never commit
/// that JSON file - see the Api README's secrets section.
/// </summary>
public class FirebasePushNotificationSender : IPushNotificationSender
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private string? _cachedAccessToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public FirebasePushNotificationSender(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _http.BaseAddress ??= new Uri("https://fcm.googleapis.com/");
        _configuration = configuration;
    }

    public async Task SendAsync(string deviceToken, string title, string body, IDictionary<string, string>? data = null, CancellationToken ct = default)
    {
        var projectId = _configuration["Firebase:ProjectId"]
            ?? throw new InvalidOperationException("Firebase:ProjectId is not configured.");
        var accessToken = await GetAccessTokenAsync(ct);

        var payload = new
        {
            message = new
            {
                token = deviceToken,
                notification = new { title, body },
                data
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1/projects/{projectId}/messages:send")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"FCM send failed ({(int)response.StatusCode}): {error}");
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cachedAccessToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
        {
            return _cachedAccessToken;
        }

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_cachedAccessToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            {
                return _cachedAccessToken;
            }

            var serviceAccountPath = _configuration["Firebase:ServiceAccountJsonPath"]
                ?? throw new InvalidOperationException("Firebase:ServiceAccountJsonPath is not configured.");
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(serviceAccountPath, ct));
            var clientEmail = doc.RootElement.GetProperty("client_email").GetString()!;
            var privateKeyPem = doc.RootElement.GetProperty("private_key").GetString()!;

            var assertion = BuildSignedJwt(clientEmail, privateKeyPem);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = assertion
                })
            };

            using var response = await _http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            response.EnsureSuccessStatusCode();

            using var tokenDoc = JsonDocument.Parse(body);
            var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = tokenDoc.RootElement.GetProperty("expires_in").GetInt32();

            _cachedAccessToken = accessToken;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);
            return accessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private static string BuildSignedJwt(string clientEmail, string privateKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var key = new RsaSecurityKey(rsa);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: clientEmail,
            claims:
            [
                new Claim("scope", "https://www.googleapis.com/auth/firebase.messaging"),
                new Claim(JwtRegisteredClaimNames.Aud, "https://oauth2.googleapis.com/token"),
                new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), System.Security.Claims.ClaimValueTypes.Integer64)
            ],
            notBefore: now,
            expires: now.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
