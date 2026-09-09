using HealingNaturalFarms.Domain.Enums;

namespace HealingNaturalFarms.Maui.Services;

/// <summary>
/// Which storefront (US / India) the shopper is browsing - the MAUI
/// counterpart of the Blazor storefront's RegionState. Persisted via
/// Preferences (not sensitive data) so it survives an app restart;
/// defaults to US on first launch, same as the web app.
/// </summary>
public class RegionState
{
    private const string StorageKey = "hnf_region";
    public RegionCode Current { get; private set; } = RegionCode.US;
    public event Action? Changed;

    public Task InitializeAsync()
    {
        var stored = Preferences.Default.Get<string?>(StorageKey, null);
        if (Enum.TryParse<RegionCode>(stored, out var region))
        {
            Current = region;
            Changed?.Invoke();
        }

        return Task.CompletedTask;
    }

    public Task SetRegionAsync(RegionCode region)
    {
        if (Current == region) return Task.CompletedTask;
        Current = region;
        Preferences.Default.Set(StorageKey, region.ToString());
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public CurrencyCode Currency => Current == RegionCode.US ? CurrencyCode.USD : CurrencyCode.INR;
}
