using HealingNaturalFarms.Domain.Enums;

namespace HealingNaturalFarms.Domain.Entities;

/// <summary>
/// A cart belongs to either a logged-in user (UserId set) or a guest
/// (GuestId set, a client-generated GUID string persisted in local
/// storage / MAUI preferences). Exactly one of the two is populated.
/// A cart is pinned to a single region for its lifetime - switching
/// storefronts starts a new cart, since prices/currency/stock aren't
/// comparable across regions.
/// </summary>
public class Cart
{
    public int Id { get; set; }
    public int? UserId { get; set; } // see the note on Address.UserId re: no User navigation here
    public string? GuestId { get; set; }
    public RegionCode Region { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public Cart? Cart { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }

    /// <summary>
    /// Price at the moment it was added, in the cart's region currency.
    /// Re-validated (not trusted) against the live ProductRegionListing
    /// price at checkout time.
    /// </summary>
    public decimal UnitPriceSnapshot { get; set; }
}
