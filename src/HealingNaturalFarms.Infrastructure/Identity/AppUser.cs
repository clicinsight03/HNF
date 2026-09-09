using HealingNaturalFarms.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace HealingNaturalFarms.Infrastructure.Identity;

/// <summary>
/// The actual ASP.NET Core Identity user stored in MySQL. Kept separate
/// from HealingNaturalFarms.Domain.Entities.ApplicationUser (a plain POCO
/// used in DTO mapping and by the MAUI client) so that neither the Domain
/// project nor the MAUI app needs to reference Identity/EF Core.
/// </summary>
public class AppUser : IdentityUser<int>
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public RegionCode PreferredRegion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AppRole : IdentityRole<int>
{
}
