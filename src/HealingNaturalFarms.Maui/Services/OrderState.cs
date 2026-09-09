using HealingNaturalFarms.Domain.Dtos;

namespace HealingNaturalFarms.Maui.Services;

/// <summary>
/// In-memory hand-off from CheckoutPage to OrderConfirmationPage, same
/// role as the Blazor storefront's OrderState (see there for why there's
/// no GET /api/orders/{id} to fetch this instead).
/// </summary>
public class OrderState
{
    public OrderDetailDto? LastOrder { get; private set; }

    public void SetLastOrder(OrderDetailDto order) => LastOrder = order;
}
