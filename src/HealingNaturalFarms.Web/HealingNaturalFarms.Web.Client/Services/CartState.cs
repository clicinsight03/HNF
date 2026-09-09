using HealingNaturalFarms.Domain.Dtos;

namespace HealingNaturalFarms.Web.Client.Services;

/// <summary>
/// Caches the current region's cart so the header badge and the cart
/// page stay in sync without both independently hitting the API. Call
/// RefreshAsync after any mutation (add/update/remove) and after a
/// region switch (a region switch starts a fresh cart server-side).
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
}
