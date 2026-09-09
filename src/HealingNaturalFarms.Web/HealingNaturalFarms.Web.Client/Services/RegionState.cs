using HealingNaturalFarms.Domain.Enums;
using Microsoft.JSInterop;

namespace HealingNaturalFarms.Web.Client.Services;

/// <summary>
/// Which storefront (US / India) the visitor is browsing. This is the
/// single source of truth the whole app reads from - catalog pages,
/// cart, and checkout all key off Region.Current rather than each
/// re-deriving it. Persisted to localStorage so a reload keeps the same
/// storefront; defaults to US on a first-ever visit (swap the default
/// for IP-based geolocation later if you want auto-detection - deliberately
/// left as an explicit choice for now rather than guessing).
/// </summary>
public class RegionState
{
    private const string StorageKey = "hnf_region";
    public RegionCode Current { get; private set; } = RegionCode.US;
    public event Action? Changed;

    public async Task InitializeAsync(IJSRuntime js)
    {
        try
        {
            var stored = await js.InvokeAsync<string?>("hnfStorage.get", StorageKey);
            if (Enum.TryParse<RegionCode>(stored, out var region))
            {
                Current = region;
                Changed?.Invoke();
            }
        }
        catch (JSException)
        {
            // Keep default.
        }
    }

    public async Task SetRegionAsync(IJSRuntime js, RegionCode region)
    {
        if (Current == region) return;
        Current = region;
        Changed?.Invoke();

        try
        {
            await js.InvokeVoidAsync("hnfStorage.set", StorageKey, region.ToString());
        }
        catch (JSException)
        {
            // Non-fatal - just won't survive a reload.
        }
    }

    public CurrencyCode Currency => Current == RegionCode.US ? CurrencyCode.USD : CurrencyCode.INR;
}
