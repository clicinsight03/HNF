namespace HealingNaturalFarms.Maui.Services;

/// <summary>
/// MAUI counterpart of the Blazor storefront's GuestIdProvider - same
/// job (a stable id for a not-yet-signed-in shopper's cart, sent as the
/// X-Guest-Id header), but backed by MAUI's Preferences API instead of
/// localStorage. Preferences is plain (unencrypted) storage, which is
/// fine here since a guest id is just a random correlation token, not a
/// credential - see AuthState for what does need SecureStorage.
/// </summary>
public class GuestIdProvider
{
    private const string StorageKey = "hnf_guest_id";
    private string _guestId = Guid.NewGuid().ToString("N");
    private bool _initialized;

    public string GetOrCreate() => _guestId;

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;
        _initialized = true;

        var stored = Preferences.Default.Get<string?>(StorageKey, null);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            _guestId = stored;
        }
        else
        {
            Preferences.Default.Set(StorageKey, _guestId);
        }

        return Task.CompletedTask;
    }
}
