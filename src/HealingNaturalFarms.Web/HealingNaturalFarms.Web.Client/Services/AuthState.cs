using HealingNaturalFarms.Domain.Enums;
using Microsoft.JSInterop;

namespace HealingNaturalFarms.Web.Client.Services;

/// <summary>
/// Minimal client-side session state - holds the JWT issued by
/// api/auth/login|register in memory (and mirrored to localStorage so a
/// reload doesn't force a re-login) for ApiClient to attach as a Bearer
/// token. This is a storefront convenience, not a security boundary in
/// itself: the API independently validates every token.
/// </summary>
public class AuthState
{
    private const string StorageKey = "hnf_auth";
    public string? AccessToken { get; private set; }
    public string? Email { get; private set; }
    public string? FirstName { get; private set; }
    public RegionCode? PreferredRegion { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public bool IsAuthenticated => AccessToken is not null && ExpiresAt > DateTimeOffset.UtcNow;
    public event Action? Changed;

    public async Task InitializeAsync(IJSRuntime js)
    {
        try
        {
            var stored = await js.InvokeAsync<string?>("hnfStorage.get", StorageKey);
            if (string.IsNullOrWhiteSpace(stored)) return;

            var parts = stored.Split('|');
            if (parts.Length != 4) return;

            AccessToken = parts[0];
            Email = parts[1];
            FirstName = parts[2];
            ExpiresAt = DateTimeOffset.TryParse(parts[3], out var exp) ? exp : null;

            if (!IsAuthenticated)
            {
                AccessToken = null;
            }

            Changed?.Invoke();
        }
        catch (JSException)
        {
            // Keep signed out.
        }
    }

    public async Task SignInAsync(IJSRuntime js, string accessToken, DateTimeOffset expiresAt, string email, string firstName, RegionCode preferredRegion)
    {
        AccessToken = accessToken;
        ExpiresAt = expiresAt;
        Email = email;
        FirstName = firstName;
        PreferredRegion = preferredRegion;
        Changed?.Invoke();

        try
        {
            await js.InvokeVoidAsync("hnfStorage.set", StorageKey, $"{accessToken}|{email}|{firstName}|{expiresAt:O}");
        }
        catch (JSException)
        {
            // Non-fatal.
        }
    }

    public async Task SignOutAsync(IJSRuntime js)
    {
        AccessToken = null;
        Email = null;
        FirstName = null;
        ExpiresAt = null;
        Changed?.Invoke();

        try
        {
            await js.InvokeVoidAsync("hnfStorage.remove", StorageKey);
        }
        catch (JSException)
        {
            // Non-fatal.
        }
    }
}
