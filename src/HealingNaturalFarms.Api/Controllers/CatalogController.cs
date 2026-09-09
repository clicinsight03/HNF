using HealingNaturalFarms.Domain.Dtos;
using HealingNaturalFarms.Domain.Entities;
using HealingNaturalFarms.Domain.Enums;
using HealingNaturalFarms.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealingNaturalFarms.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Browse the catalog for a region. Region is mandatory and drives
    /// both which products show up (a listing must exist for the region)
    /// and which price/currency is returned - the same product can be
    /// priced differently, or unavailable, in each storefront.
    /// </summary>
    [HttpGet("products")]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> GetProducts(
        [FromQuery] RegionCode region,
        [FromQuery] ProductType? productType,
        [FromQuery] int? categoryId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query =
            from p in db.Products
            join l in db.ProductRegionListings on p.Id equals l.ProductId
            where p.IsActive && l.Region == region
            select new { p, l };

        if (productType is not null)
        {
            query = query.Where(x => x.p.ProductType == productType);
        }

        if (categoryId is not null)
        {
            query = query.Where(x => x.p.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => EF.Functions.Like(x.p.Name, $"%{search}%"));
        }

        var totalCount = await query.CountAsync(ct);

        var pageItems = await query
            .OrderBy(x => x.p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.p.Id,
                x.p.Sku,
                x.p.Name,
                x.p.Slug,
                x.p.ProductType,
                Thumbnail = x.p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                x.l.Price,
                x.l.CompareAtPrice,
                x.l.Currency,
                x.l.StockQuantity
            })
            .ToListAsync(ct);

        var items = pageItems
            .Select(x => new ProductListItemDto(x.Id, x.Sku, x.Name, x.Slug, x.ProductType, x.Thumbnail, x.Price, x.CompareAtPrice, x.Currency, x.StockQuantity > 0))
            .ToList();

        return Ok(new PagedResult<ProductListItemDto>(items, page, pageSize, totalCount));
    }

    [HttpGet("products/{slug}")]
    public async Task<ActionResult<ProductDetailDto>> GetProductBySlug(string slug, [FromQuery] RegionCode region, CancellationToken ct)
    {
        var product = await db.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.RegionListings)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive, ct);

        if (product is null) return NotFound();

        var listing = product.RegionListings.FirstOrDefault(l => l.Region == region);
        if (listing is null) return NotFound(new { message = $"Not offered in region {region}." });

        return Ok(new ProductDetailDto(
            product.Id, product.Sku, product.Name, product.Slug,
            product.ShortDescription, product.Description, product.ProductType,
            product.Category!.Name, product.Category.Slug,
            product.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
            listing.Price, listing.CompareAtPrice, listing.Currency, listing.StockQuantity, listing.StockQuantity > 0 && listing.IsAvailable,
            product.SunlightNeeds, product.WateringFrequency, product.MatureHeightCm, product.PotSizeCm, product.IsIndoor, product.IsPetSafe,
            product.UnitOfSale, product.IsOrganic, product.IsSeasonal));
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories([FromQuery] ProductType? productType, CancellationToken ct)
    {
        var query = db.Categories.AsQueryable();
        if (productType is not null)
        {
            query = query.Where(c => c.ProductType == productType);
        }

        var categories = await query
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.ProductType))
            .ToListAsync(ct);

        return Ok(categories);
    }
}
