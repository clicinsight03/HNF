using HealingNaturalFarms.Maui.Services;

namespace HealingNaturalFarms.Maui;

public partial class App : Application
{
    public App(RegionState region, AuthState auth, GuestIdProvider guestId)
    {
        InitializeComponent();

        // Load anything persisted from a previous run (SecureStorage /
        // Preferences - the MAUI equivalent of the Blazor storefront's
        // localStorage-backed InitializeAsync calls) before the first
        // page renders, so Shop/Cart/Login already reflect who the
        // visitor is and which storefront they last used.
        _ = InitializeAsync(region, auth, guestId);
    }

    private static async Task InitializeAsync(RegionState region, AuthState auth, GuestIdProvider guestId)
    {
        await guestId.InitializeAsync();
        await region.InitializeAsync();
        await auth.InitializeAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
