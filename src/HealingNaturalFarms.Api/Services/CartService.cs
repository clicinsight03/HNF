using HealingNaturalFarms.Domain.Dtos;
using HealingNaturalFarms.Domain.Entities;
using HealingNaturalFarms.Domain.Enums;
using HealingNaturalFarms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HealingNaturalFarms.Api.Services;

public interface ICartService
{
    Task<CartDto> GetOrCreateCartAsync(int? userId, string? guestId, RegionCode region, CancellationToken ct = default);
    Task<CartDto> AddItemAsync(int cartId, int productId, int quantity, CancellationToken ct = default);
    Task<CartDto> UpdateItemAsync(int cartId, int productId, int quantity, CancellationToken ct = default);
    Task<CartDto> RemoveItemAsync(int cartId, int productId, CancellationToken ct = default);
    Task<CartDto> GetCartDtoAsync(int cartId, CancellationToken ct = default);
}

public class CartService(AppDbContext db) : ICartService
{
    public async Task<CartDto> GetOrCreateCartAsync(int? userId, string? guestId, RegionCode region, CancellationToken ct = default)
    {
        Cart? cart = userId is not null
            ? await db.Carts.FirstOrDefaultAsync(c => c.UserId == userId && c.Region == region, ct)
            : await db.Carts.FirstOrDefaultAsync(c => c.GuestId == guestId && c.Region == region, ct);

        if (cart is null)
        {
            cart = new Cart { UserId = userId, GuestId = userId is null ? guestId : null, Region = region };
            db.Carts.Add(cart);
            await db.SaveChangesAsync(ct);
        }

        return await GetCartDtoAsync(cart.Id, ct);
    }

    public async Task<CartDto> AddItemAsync(int cartId, int productId, int quantity, CancellationToken ct = default)
    {
        var cart = await GetCartOrThrowAsync(cartId, ct);
        var listing = await GetActiveListingOrThrowAsync(productId, cart.Region, ct);

        var existing = await db.CartItems.FirstOrDefaultAsync(i => i.CartId == cartId && i.ProductId == productId, ct);
        if (existing is null)
        {
            db.CartItems.Add(new CartItem
            {
                CartId = cartId,
                ProductId = productId,
                Quantity = quantity,
                UnitPriceSnapshot = listing.Price
            });
        }
        else
        {
            existing.Quantity += quantity;
            existing.UnitPriceSnapshot = listing.Price; // refresh snapshot to current price
        }

        cart.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetCartDtoAsync(cartId, ct);
    }

    public async Task<CartDto> UpdateItemAsync(int cartId, int productId, int quantity, CancellationToken ct = default)
    {
        var item = await db.CartItems.FirstOrDefaultAsync(i => i.CartId == cartId && i.ProductId == productId, ct)
            ?? throw new KeyNotFoundException("Cart item not found.");

        if (quantity <= 0)
        {
            db.CartItems.Remove(item);
        }
        else
        {
            item.Quantity = quantity;
        }

        await db.SaveChangesAsync(ct);
        return await GetCartDtoAsync(cartId, ct);
    }

    public async Task<CartDto> RemoveItemAsync(int cartId, int productId, CancellationToken ct = default)
    {
        var item = await db.CartItems.FirstOrDefaultAsync(i => i.CartId == cartId && i.ProductId == productId, ct);
        if (item is not null)
        {
            db.CartItems.Remove(item);
            await db.SaveChangesAsync(ct);
        }

        return await GetCartDtoAsync(cartId, ct);
    }

    public async Task<CartDto> GetCartDtoAsync(int cartId, CancellationToken ct = default)
    {
        var cart = await GetCartOrThrowAsync(cartId, ct);

        var items = await (
            from ci in db.CartItems
            where ci.CartId == cartId
            join p in db.Products on ci.ProductId equals p.Id
            select new { ci, p, Image = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault() }
        ).ToListAsync(ct);

        var itemDtos = items
            .Select(x => new CartItemDto(x.p.Id, x.p.Name, x.Image, x.ci.UnitPriceSnapshot, x.ci.Quantity, x.ci.UnitPriceSnapshot * x.ci.Quantity))
            .ToList();

        var currency = cart.Region == RegionCode.US ? CurrencyCode.USD : CurrencyCode.INR;
        return new CartDto(cart.Id, cart.Region, currency, itemDtos, itemDtos.Sum(i => i.LineTotal));
    }

    private async Task<Cart> GetCartOrThrowAsync(int cartId, CancellationToken ct) =>
        await db.Carts.FirstOrDefaultAsync(c => c.Id == cartId, ct)
            ?? throw new KeyNotFoundException($"Cart {cartId} not found.");

    private async Task<ProductRegionListing> GetActiveListingOrThrowAsync(int productId, RegionCode region, CancellationToken ct)
    {
        var listing = await db.ProductRegionListings
            .FirstOrDefaultAsync(l => l.ProductId == productId && l.Region == region, ct)
            ?? throw new InvalidOperationException($"Product {productId} is not offered in region {region}.");

        if (!listing.IsAvailable || listing.StockQuantity <= 0)
        {
            throw new InvalidOperationException($"Product {productId} is out of stock in region {region}.");
        }

        return listing;
    }
}
