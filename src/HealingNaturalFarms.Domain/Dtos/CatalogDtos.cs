using HealingNaturalFarms.Domain.Enums;

namespace HealingNaturalFarms.Domain.Dtos;

public record ProductListItemDto(
    int Id,
    string Sku,
    string Name,
    string Slug,
    ProductType ProductType,
    string? ThumbnailUrl,
    decimal Price,
    decimal? CompareAtPrice,
    CurrencyCode Currency,
    bool InStock);

public record ProductDetailDto(
    int Id,
    string Sku,
    string Name,
    string Slug,
    string? ShortDescription,
    string? Description,
    ProductType ProductType,
    string CategoryName,
    string CategorySlug,
    IReadOnlyList<string> ImageUrls,
    decimal Price,
    decimal? CompareAtPrice,
    CurrencyCode Currency,
    int StockQuantity,
    bool InStock,
    // Plant-specific (null for Produce)
    SunlightNeeds? SunlightNeeds,
    string? WateringFrequency,
    decimal? MatureHeightCm,
    decimal? PotSizeCm,
    bool? IsIndoor,
    bool? IsPetSafe,
    // Produce-specific (null for Plant)
    UnitOfSale? UnitOfSale,
    bool? IsOrganic,
    bool? IsSeasonal);

public record CategoryDto(int Id, string Name, string Slug, ProductType ProductType);

public record ProductCatalogQuery(
    RegionCode Region,
    ProductType? ProductType = null,
    int? CategoryId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 24);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
