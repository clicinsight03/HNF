using System.Collections.ObjectModel;
using HealingNaturalFarms.Domain.Enums;
using HealingNaturalFarms.Maui.Converters;
using HealingNaturalFarms.Maui.Services;

namespace HealingNaturalFarms.Maui.Pages;

/// <summary>
/// Cart-line display shape for the CollectionView. CartItemDto (the DTO
/// the API returns) doesn't carry its own CurrencyCode - only the parent
/// CartDto does - so prices are pre-formatted here in code-behind
/// (where CartState.Cart.Currency is in scope) rather than via an XAML
/// converter that would otherwise need a MultiBinding to reach both
/// values. See MoneyConverter's doc comment for the fuller reasoning.
/// </summary>
public class CartLineDisplay
{
    public int ProductId { get; init; }
    public string Name { get; init; } = "";
    public string? ThumbnailUrl { get; init; }
    public int Quantity { get; init; }
    public string LineTotalDisplay { get; init; } = "";
}

public partial class CartPage : ContentPage
{
    private readonly CartState _cart;

    private readonly ObservableCollection<CartLineDisplay> _lines = new();

    public CartPage(CartState cart)
    {
        InitializeComponent();
        _cart = cart;
        ItemsView.ItemsSource = _lines;
        _cart.Changed += OnCartChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _cart.RefreshAsync();
    }

    private void OnCartChanged() => MainThread.BeginInvokeOnMainThread(RenderCart);

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await _cart.RefreshAsync();
        CartRefreshView.IsRefreshing = false;
    }

    private void RenderCart()
    {
        _lines.Clear();
        var cart = _cart.Cart;

        if (cart is not null)
        {
            foreach (var item in cart.Items)
            {
                _lines.Add(new CartLineDisplay
                {
                    ProductId = item.ProductId,
                    Name = item.Name,
                    ThumbnailUrl = item.ThumbnailUrl,
                    Quantity = item.Quantity,
                    LineTotalDisplay = MoneyConverter.Format(item.LineTotal, cart.Currency)
                });
            }
        }

        var hasItems = cart is not null && cart.Items.Count > 0;
        SummaryFrame.IsVisible = hasItems;
        if (hasItems)
        {
            SubtotalLabel.Text = MoneyConverter.Format(cart!.Subtotal, cart.Currency);
        }
    }

    private async void OnIncrementClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: int productId })
        {
            var current = _cart.Cart?.Items.FirstOrDefault(i => i.ProductId == productId);
            if (current is not null)
            {
                await _cart.UpdateQuantityAsync(productId, current.Quantity + 1);
            }
        }
    }

    private async void OnDecrementClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: int productId })
        {
            var current = _cart.Cart?.Items.FirstOrDefault(i => i.ProductId == productId);
            if (current is not null)
            {
                await _cart.UpdateQuantityAsync(productId, current.Quantity - 1);
            }
        }
    }

    private async void OnRemoveTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TapGestureRecognizer { CommandParameter: int productId })
        {
            await _cart.UpdateQuantityAsync(productId, 0);
        }
    }

    private async void OnContinueShoppingClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//shop");
    }

    private async void OnCheckoutClicked(object? sender, EventArgs e)
    {
        if (_cart.Cart is null || _cart.Cart.Items.Count == 0) return;
        await Shell.Current.GoToAsync(nameof(CheckoutPage));
    }
}
