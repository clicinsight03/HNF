using HealingNaturalFarms.Maui.Converters;
using HealingNaturalFarms.Maui.Services;
using OrderStateService = HealingNaturalFarms.Maui.Services.OrderState;

namespace HealingNaturalFarms.Maui.Pages;

public partial class OrderConfirmationPage : ContentPage
{
    private readonly OrderStateService _orderState;

    public OrderConfirmationPage(OrderStateService orderState)
    {
        InitializeComponent();
        _orderState = orderState;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var order = _orderState.LastOrder;
        if (order is null)
        {
            EmptyState.IsVisible = true;
            OrderDetailsSection.IsVisible = false;
            return;
        }

        EmptyState.IsVisible = false;
        OrderDetailsSection.IsVisible = true;

        OrderNumberLabel.Text = $"Order {order.OrderNumber} · placed {order.CreatedAt.ToLocalTime():f}";

        ItemsStack.Children.Clear();
        foreach (var item in order.Items)
        {
            ItemsStack.Children.Add(new Label { Text = $"{item.Name} x{item.Quantity}  —  {MoneyConverter.Format(item.LineTotal, order.Currency)}" });
        }
        ItemsStack.Children.Add(new BoxView { HeightRequest = 1, Color = Colors.LightGray, Margin = new Thickness(0, 6) });
        ItemsStack.Children.Add(new Label { Text = $"Subtotal: {MoneyConverter.Format(order.Subtotal, order.Currency)}" });
        ItemsStack.Children.Add(new Label { Text = $"Shipping: {MoneyConverter.Format(order.ShippingCost, order.Currency)}" });
        ItemsStack.Children.Add(new Label { Text = $"Tax: {MoneyConverter.Format(order.Tax, order.Currency)}" });
        ItemsStack.Children.Add(new Label { Text = $"Total: {MoneyConverter.Format(order.Total, order.Currency)}", FontAttributes = FontAttributes.Bold });

        ShippingStack.Children.Clear();
        ShippingStack.Children.Add(new Label { Text = "Shipping to", FontAttributes = FontAttributes.Bold });
        ShippingStack.Children.Add(new Label { Text = order.ShippingAddress.FullName });
        ShippingStack.Children.Add(new Label { Text = order.ShippingAddress.Line1 });
        if (!string.IsNullOrWhiteSpace(order.ShippingAddress.Line2))
        {
            ShippingStack.Children.Add(new Label { Text = order.ShippingAddress.Line2 });
        }
        ShippingStack.Children.Add(new Label { Text = $"{order.ShippingAddress.City}, {order.ShippingAddress.State} {order.ShippingAddress.PostalCode}" });
        ShippingStack.Children.Add(new Label { Text = order.ShippingAddress.Country });
    }

    private async void OnContinueShoppingClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//shop");
    }
}
