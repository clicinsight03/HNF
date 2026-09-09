using System.Net.Http.Json;
using HealingNaturalFarms.Domain.Dtos;
using HealingNaturalFarms.Domain.Enums;

namespace HealingNaturalFarms.Maui.Services;

/// <summary>
/// Same REST surface as the Blazor storefront's ApiClient (see
/// src/HealingNaturalFarms.Web/.../Services/ApiClient.cs) - kept as a
/// near-identical copy rather than a shared project so each client can
/// evolve its auth/guest-header plumbing independently (MAUI's
/// SecureStorage-backed AuthState vs. the web app's localStorage one).
/// If this drifts in practice, promoting it into Domain (which both
/// already reference) is the natural next step.
/// </summary>
public class ApiClient(HttpClient http, GuestIdProvider guestId, AuthState authState)
{
    private void ApplyGuestAndAuthHeaders()
    {
        http.DefaultRequestHeaders.Remove("X-Guest-Id");
        if (!authState.IsAuthenticated)
        {
            http.DefaultRequestHeaders.Add("X-Guest-Id", guestId.GetOrCreate());
        }

        http.DefaultRequestHeaders.Authorization = authState.IsAuthenticated
            ? new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authState.AccessToken)
            : null;
    }

    public async Task<PagedResult<ProductListItemDto>?> GetProductsAsync(RegionCode region, ProductType? type, int? categoryId, string? search, int page = 1, CancellationToken ct = default)
    {
        var url = $"api/catalog/products?region={region}&page={page}";
        if (type is not null) url += $"&productType={type}";
        if (categoryId is not null) url += $"&categoryId={categoryId}";
        if (!string.IsNullOrWhiteSpace(search)) url += $"&search={Uri.EscapeDataString(search)}";
        return await http.GetFromJsonAsync<PagedResult<ProductListItemDto>>(url, ct);
    }

    public Task<ProductDetailDto?> GetProductAsync(string slug, RegionCode region, CancellationToken ct = default) =>
        http.GetFromJsonAsync<ProductDetailDto>($"api/catalog/products/{slug}?region={region}", ct);

    public Task<IReadOnlyList<CategoryDto>?> GetCategoriesAsync(ProductType? type, CancellationToken ct = default) =>
        http.GetFromJsonAsync<IReadOnlyList<CategoryDto>>(type is null ? "api/catalog/categories" : $"api/catalog/categories?productType={type}", ct);

    public async Task<CartDto?> GetCartAsync(RegionCode region, CancellationToken ct = default)
    {
        ApplyGuestAndAuthHeaders();
        return await http.GetFromJsonAsync<CartDto>($"api/cart?region={region}", ct);
    }

    public async Task<CartDto?> AddToCartAsync(RegionCode region, int productId, int quantity, CancellationToken ct = default)
    {
        ApplyGuestAndAuthHeaders();
        var response = await http.PostAsJsonAsync($"api/cart/items?region={region}", new AddCartItemRequest(productId, quantity), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CartDto>(cancellationToken: ct);
    }

    public async Task<CartDto?> UpdateCartItemAsync(int cartId, int productId, int quantity, CancellationToken ct = default)
    {
        ApplyGuestAndAuthHeaders();
        var response = await http.PutAsJsonAsync($"api/cart/{cartId}/items/{productId}", new UpdateCartItemRequest(quantity), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CartDto>(cancellationToken: ct);
    }

    public async Task<CreateCheckoutResponse?> CreateCheckoutAsync(CreateCheckoutRequest request, CancellationToken ct = default)
    {
        ApplyGuestAndAuthHeaders();
        var response = await http.PostAsJsonAsync("api/checkout", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateCheckoutResponse>(cancellationToken: ct);
    }

    public async Task<OrderDetailDto?> ConfirmPaymentAsync(ConfirmPaymentRequest request, CancellationToken ct = default)
    {
        ApplyGuestAndAuthHeaders();
        var response = await http.PostAsJsonAsync("api/checkout/confirm", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDetailDto>(cancellationToken: ct);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/auth/register", request, ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
    }

    public async Task RegisterDeviceAsync(RegisterDeviceRequest request, CancellationToken ct = default)
    {
        ApplyGuestAndAuthHeaders();
        await http.PostAsJsonAsync("api/notifications/devices", request, ct);
    }
}
