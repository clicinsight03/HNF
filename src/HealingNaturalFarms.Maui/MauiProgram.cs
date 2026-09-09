using HealingNaturalFarms.Maui.Pages;
using HealingNaturalFarms.Maui.Services;
using Microsoft.Extensions.Logging;

namespace HealingNaturalFarms.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // No custom fonts are bundled (Resources/Fonts is empty) - this
        // sandbox can't fabricate real .ttf binaries, and referencing a
        // font file that doesn't exist would only fail at runtime instead
        // of at build time. Drop your own .ttf files into Resources/Fonts
        // and register them here with .ConfigureFonts(...) if you want a
        // custom typeface; Styles.xaml's font-family setters were written
        // to fall back cleanly to the OS default in the meantime.
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // ---- HttpClient -----------------------------------------------
        // Same API surface the Blazor storefront talks to. Update this
        // base address per environment (see appsettings-equivalent notes
        // in README.md - MAUI has no wwroot/appsettings.json, so the
        // simplest approach is a compile-time constant here, or reading
        // from Preferences if you want it changeable without a rebuild.
        //
        // NOTE: "localhost" from an Android emulator does NOT mean the
        // host machine - use 10.0.2.2 (standard Android emulator alias)
        // or your machine's LAN IP. iOS simulator can use localhost
        // directly. A real device needs your machine's LAN IP either way.
        builder.Services.AddSingleton(sp => new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl)
        });

        // ---- App services ------------------------------------------------
        builder.Services.AddSingleton<GuestIdProvider>();
        builder.Services.AddSingleton<RegionState>();
        builder.Services.AddSingleton<AuthState>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<CartState>();
        builder.Services.AddSingleton<OrderState>();
        builder.Services.AddSingleton<IPushTokenService, PushTokenService>();
        builder.Services.AddSingleton<PushRegistrationService>();

        // ---- Pages (transient - Shell creates a new instance per navigation) --
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ShopPage>();
        builder.Services.AddTransient<ProductDetailPage>();
        builder.Services.AddTransient<CartPage>();
        builder.Services.AddTransient<CheckoutPage>();
        builder.Services.AddTransient<OrderConfirmationPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

/// <summary>
/// A single place to change the API's base URL per build/environment.
/// A production app would typically vary this per build configuration
/// (Debug/Release) or read it from a small non-secret config file bundled
/// as a MauiAsset - kept as a plain constant here to stay dependency-free
/// and obvious to find.
/// </summary>
public static class ApiConfig
{
    public const string BaseUrl =
#if ANDROID
        "https://10.0.2.2:7156/";
#else
        "https://localhost:7156/";
#endif
}
