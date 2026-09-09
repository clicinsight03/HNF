using HealingNaturalFarms.Domain.Enums;

namespace HealingNaturalFarms.Maui.Services;

/// <summary>
/// MAUI counterpart of the Blazor storefront's AuthState - holds the JWT
/// issued by api/auth/login|register for ApiClient to attach as a
/// Bearer token. Unlike the web app (which mirrors it to localStorage,
/// a reasonable place for a short-lived web session token), this uses
/// SecureStorage - MAUI's Keychain (iOS)/Keystore (Android)-backed
/// encrypted storage - since a mobile app's session token typically
/// lives far longer between app launches than a browser tab does.
/// </summary>
public class AuthState
{
    private const string TokenKey = "hnf_auth_token";
    private const string EmailKey = "hnf_auth_email";
    private const string NameKey = "hnf_auth_name";
    private const string RegionKey = "hnf_auth_region";
    private const string ExpiresKey = "hnf_auth_expires";

    public string? AccessToken { get; private set; }
    public string? Email { get; private set; }
    public string? FirstName { get; private set; }
    public RegionCode? PreferredRegion { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public bool IsAuthenticated => AccessToken is not null && ExpiresAt > DateTimeOffset.UtcNow;
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        try
        {
            var token = await SecureStorage.Default.GetAsync(TokenKey);
            var expiresRaw = await SecureStorage.Default.GetAsync(ExpiresKey);
            if (string.IsNullOrWhiteSpace(token) || !DateTimeOffset.TryParse(expiresRaw, out var expiresAt) || expiresAt <= DateTimeOffset.UtcNow)
            {
                return; // nothing usable saved, or the session has since expired
            }

            AccessToken = token;
            ExpiresAt = expiresAt;
            Email = await SecureStorage.Default.GetAsync(EmailKey);
            FirstName = await SecureStorage.Default.GetAsync(NameKey);
            if (Enum.TryParse<RegionCode>(await SecureStorage.Default.GetAsync(RegionKey), out var region))
            {
                PreferredRegion = region;
            }

            Changed?.Invoke();
        }
        catch (Exception)
        {
            // SecureStorage can throw on some Android devices if the
            // keystore was reset (e.g. after a backup restore onto new
            // hardware) - treat that the same as "nothing saved".
            ClearLocalState();
        }
    }

    public async Task SignInAsync(string accessToken, DateTimeOffset expiresAt, string email, string firstName, RegionCode preferredRegion)
    {
        AccessToken = accessToken;
        ExpiresAt = expiresAt;
        Email = email;
        FirstName = firstName;
        PreferredRegion = preferredRegion;
        Changed?.Invoke();

        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, accessToken);
            await SecureStorage.Default.SetAsync(ExpiresKey, expiresAt.ToString("O"));
            await SecureStorage.Default.SetAsync(EmailKey, email);
            await SecureStorage.Default.SetAsync(NameKey, firstName);
            await SecureStorage.Default.SetAsync(RegionKey, preferredRegion.ToString());
        }
        catch (Exception)
        {
            // Non-fatal: the session still works for the rest of this
            // app run, it just won't survive a restart.
        }
    }

    public Task SignOutAsync()
    {
        ClearLocalState();
        SecureStorage.Default.RemoveAll();
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    private void ClearLocalState()
    {
        AccessToken = null;
        Email = null;
        FirstName = null;
        ExpiresAt = null;
        PreferredRegion = null;
    }
}
