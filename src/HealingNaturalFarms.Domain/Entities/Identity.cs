using HealingNaturalFarms.Domain.Enums;

namespace HealingNaturalFarms.Domain.Entities;

/// <summary>
/// The storefront's view of a user. This is a plain POCO, not an ASP.NET
/// Identity type - Identity's own user/role classes live in the
/// Infrastructure project (which already depends on EF Core + Identity
/// packages) and are mapped to/from this shape. Keeping Domain free of
/// Identity/EF references means the .NET MAUI client can reference
/// Domain directly without pulling in server-only packages.
/// Region is a *preference*, not an enforcement mechanism - a US account
/// can still be served the India catalog if they explicitly switch, but
/// this is what pre-selects their storefront on login.
/// </summary>
public class ApplicationUser
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public RegionCode PreferredRegion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Note on the UserId FK here (and on <see cref="DeviceRegistration"/>):
/// there is deliberately no `User` navigation property to
/// <see cref="ApplicationUser"/>. The actual EF Core / Identity user
/// entity is HealingNaturalFarms.Infrastructure.Identity.AppUser, not
/// this Domain-layer POCO - Infrastructure configures the real
/// relationship against AppUser by UserId. ApplicationUser exists purely
/// as a plain, EF-agnostic shape for DTO mapping and for the MAUI client.
/// </summary>
public class Address
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public required string FullName { get; set; }
    public required string Line1 { get; set; }
    public string? Line2 { get; set; }
    public required string City { get; set; }
    public required string State { get; set; }
    public required string PostalCode { get; set; }
    public required string Country { get; set; } // ISO 3166-1 alpha-2, e.g. "US" / "IN"
    public string? Phone { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// A push-notification-capable device registered from the MAUI app.
/// Stores the FCM registration token; see HealingNaturalFarms.Payments
/// (no - see the Api's NotificationsController) for how this is used to
/// send order-status and promo pushes.
/// </summary>
public class DeviceRegistration
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public DevicePlatform Platform { get; set; }
    public required string PushToken { get; set; }
    public string? DeviceModel { get; set; }
    public string? AppVersion { get; set; }
    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}
