using Microsoft.JSInterop;

namespace HealingNaturalFarms.Web.Client.Services;

/// <summary>
/// A guest cart needs a stable id across page loads before the visitor
/// logs in. Generated once in memory immediately (so the very first
/// render already has something to send), then reconciled against
/// localStorage as soon as JS interop becomes available (it isn't during
/// static server prerendering) - see InitializeAsync, called from
/// MainLayout.OnAfterRenderAsync.
/// </summary>
public class GuestIdProvider
{
    private const string StorageKey = "hnf_guest_id";
    private string _guestId = Guid.NewGuid().ToString("N");
    private bool _initialized;

    public string GetOrCreate() => _guestId;

    public async Task InitializeAsync(IJSRuntime js)
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            var stored = await js.InvokeAsync<string?>("hnfStorage.get", StorageKey);
            if (!string.IsNullOrWhiteSpace(stored))
            {
                _guestId = stored;
            }
            else
            {
                await js.InvokeVoidAsync("hnfStorage.set", StorageKey, _guestId);
            }
        }
        catch (JSException)
        {
            // Storage unavailable (privacy mode, etc.) - keep the in-memory id.
        }
    }
}
