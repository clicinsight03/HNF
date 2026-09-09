using HealingNaturalFarms.Domain.Dtos;

namespace HealingNaturalFarms.Maui.Services;

/// <summary>
/// MAUI counterpart of the Blazor storefront's CartState - identical
/// contract (see the web version for the fuller rationale): caches the
/// current region's cart, notifies subscribers (the Cart tab badge,
/// CartPage, CheckoutPage) via Changed, and exposes ClearAsync for the
/// same "no server-side clear-cart endpoint" reason.
/// </summary>
public class CartState(ApiClient api, RegionState region)
{
    public CartDto? Cart { get; private set; }
    public int ItemCount => Cart?.Items.Sum(i => i.Quantity) ?? 0;
    public event Action? Changed;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        Cart = await api.GetCartAsync(region.Current, ct);
        Changed?.Invoke();
    }

    public async Task AddAsync(int productId, int quantity, CancellationToken ct = default)
    {
        Cart = await api.AddToCartAsync(region.Current, productId, quantity, ct);
        Changed?.Invoke();
    }

    public async Task UpdateQuantityAsync(int productId, int quantity, CancellationToken ct = default)
    {
        if (Cart is null) return;
        Cart = await api.UpdateCartItemAsync(Cart.CartId, productId, quantity, ct);
        Changed?.Invoke();
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        if (Cart is null || Cart.Items.Count == 0) return;

        var cartId = Cart.CartId;
        foreach (var item in Cart.Items.ToList())
        {
            Cart = await api.UpdateCartItemAsync(cartId, item.ProductId, 0, ct);
        }

        Changed?.Invoke();
    }
}
