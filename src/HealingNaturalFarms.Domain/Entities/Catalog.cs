using HealingNaturalFarms.Domain.Enums;

namespace HealingNaturalFarms.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public ProductType ProductType { get; set; }
    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

/// <summary>
/// A single sellable item. Common fields apply to both plants and produce;
/// the type-specific fields below are nullable and only populated for the
/// matching <see cref="ProductType"/>. Region-specific price/stock/
/// availability lives in <see cref="ProductRegionListing"/>, not here -
/// a product can be sold in the US only, India only, or both, at
/// different prices.
/// </summary>
public class Product
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? ShortDescription { get; set; }
    public string? Description { get; set; }
    public ProductType ProductType { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductRegionListing> RegionListings { get; set; } = new List<ProductRegionListing>();

    // --- Plant-specific (null when ProductType == Produce) ---
    public SunlightNeeds? SunlightNeeds { get; set; }
    public string? WateringFrequency { get; set; }
    public decimal? MatureHeightCm { get; set; }
    public decimal? PotSizeCm { get; set; }
    public bool? IsIndoor { get; set; }
    public bool? IsPetSafe { get; set; }

    // --- Produce-specific (null when ProductType == Plant) ---
    public UnitOfSale? UnitOfSale { get; set; }
    public bool? IsOrganic { get; set; }
    public bool? IsSeasonal { get; set; }
    public int? HarvestSeasonStartMonth { get; set; } // 1-12
    public int? HarvestSeasonEndMonth { get; set; }    // 1-12
}

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public required string Url { get; set; }
    public int SortOrder { get; set; }
    public string? AltText { get; set; }
}

/// <summary>
/// Region-scoped pricing, currency, stock and availability for a product.
/// This is the join point between the shared catalog and the two
/// storefronts (US / India) - a row's absence means the product isn't
/// offered in that region at all.
/// </summary>
public class ProductRegionListing
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public RegionCode Region { get; set; }
    public CurrencyCode Currency { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int StockQuantity { get; set; }
    public bool IsAvailable { get; set; } = true;
}
