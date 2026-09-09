using HealingNaturalFarms.Domain.Dtos;
// Both HealingNaturalFarms.Domain.Enums and Microsoft.Maui.Devices (a
// MAUI implicit global using) declare a "DevicePlatform" type - alias
// ours explicitly so every reference below is unambiguous rather than
// relying on the compiler to guess which one a bare "DevicePlatform"
// means.
using DomainDevicePlatform = HealingNaturalFarms.Domain.Enums.DevicePlatform;

namespace HealingNaturalFarms.Maui.Services;

/// <summary>
/// Orchestrates device push registration: gets this device's push token
/// (see PushTokenService for why that's currently a documented stub),
/// works out DevicePlatform from the running OS, and posts it to
/// api/notifications/devices so the server can target this device from
/// FirebasePushNotificationSender. Called once at startup (see
/// App.xaml.cs) and safe to call again any time the app resumes - the
/// endpoint upserts by token, so re-registering the same token is a
/// no-op server-side.
///
/// Best-effort throughout: a signed-out guest still gets registered
/// (RegisterDeviceRequest doesn't require auth - see
/// NotificationsController), and any failure here (no token yet, no
/// network, server unreachable) is swallowed rather than surfacing to
/// the shopper, since push registration is a nice-to-have, never a
/// blocker for using the app.
/// </summary>
public class PushRegistrationService(IPushTokenService pushTokenService, ApiClient api)
{
    public async Task RegisterIfPossibleAsync(CancellationToken ct = default)
    {
        try
        {
            var token = await pushTokenService.GetDeviceTokenAsync(ct);
            if (string.IsNullOrWhiteSpace(token))
            {
                return; // no push package wired up yet - see PushTokenService's doc comment
            }

            var platform = DeterminePlatform();
            if (platform is null)
            {
                return; // desktop/other targets aren't in this app's push scope
            }

            await api.RegisterDeviceAsync(new RegisterDeviceRequest(
                platform.Value,
                token,
                DeviceModel: DeviceInfo.Current.Model,
                AppVersion: AppInfo.Current.VersionString), ct);
        }
        catch (Exception)
        {
            // Never let a push-registration failure affect app startup or
            // any user-facing flow.
        }
    }

    private static DomainDevicePlatform? DeterminePlatform() =>
        DeviceInfo.Current.Platform.ToString() switch
        {
            "Android" => DomainDevicePlatform.Android,
            "iOS" => DomainDevicePlatform.iOS,
            _ => null // MacCatalyst, Windows, etc. - this app's RegisterDeviceRequest only models Android/iOS
        };
}
