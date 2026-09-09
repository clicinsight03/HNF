namespace HealingNaturalFarms.Maui.Services;

public interface IPushTokenService
{
    /// <summary>
    /// Returns this device's current push token (an FCM registration
    /// token on Android, an APNs-derived FCM token on iOS), or null if
    /// one isn't available yet / push isn't wired up on this platform.
    /// </summary>
    Task<string?> GetDeviceTokenAsync(CancellationToken ct = default);
}

/// <summary>
/// DOCUMENTED GAP, same spirit as the missing font/icon files elsewhere
/// in this project: this sandbox has no NuGet access to add and verify a
/// push messaging package against, so this returns null on every
/// platform rather than pretending to work. What's real and complete is
/// everything downstream of a token: PushRegistrationService below,
/// api/notifications/devices on the server
/// (HealingNaturalFarms.Api/Controllers/NotificationsController.cs), and
/// FirebasePushNotificationSender's FCM v1 send. Only "how do I obtain
/// the token from the OS" is stubbed.
///
/// To make this real on your own machine:
///
/// Android - add a Firebase Messaging binding (e.g. the community
/// `Plugin.Firebase.CloudMessaging` package, or Google's own
/// Xamarin/MAUI Firebase bindings) and a `google-services.json` from the
/// same Firebase project the server's Firebase:ProjectId points at
/// (Firebase Console > Project Settings > your Android app > download
/// google-services.json into this project's Platforms/Android folder,
/// and reference it from the .csproj per that package's setup docs).
/// The package's API then hands you the current FCM registration token
/// (and a refresh callback - tokens can rotate) - return it from
/// GetDeviceTokenAsync below.
///
/// iOS - two things, both needed together: (1) enable the "Push
/// Notifications" capability and call
/// `UIApplication.SharedApplication.RegisterForRemoteNotifications()`
/// (this part needs no extra NuGet - it's in the base .NET for iOS
/// bindings), capturing the raw APNs device token in your
/// AppDelegate's `RegisteredForRemoteNotifications` override; (2) a
/// Firebase Messaging iOS binding to exchange that raw APNs token for an
/// FCM registration token - FCM (the same server-side integration this
/// app already uses for Android) needs an FCM token, not the bare APNs
/// one, to relay a push to an iOS device. Skipping (2) and registering
/// the raw APNs token with this app's RegisterDeviceAsync would silently
/// fail server-side sends to iOS.
/// </summary>
public class PushTokenService : IPushTokenService
{
    public Task<string?> GetDeviceTokenAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);
}
