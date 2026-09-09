using HealingNaturalFarms.Domain.Dtos;

namespace HealingNaturalFarms.Web.Client.Services;

/// <summary>
/// In-memory hand-off from Checkout.razor to OrderConfirmation.razor for
/// the just-placed order. There's no GET /api/orders/{id} endpoint yet
/// (the API returns the full OrderDetailDto directly from
/// POST /api/checkout/confirm), so rather than add one purely to support
/// a page refresh, the confirmation page reads from this scoped service
/// and falls back to "go find it in your order history" messaging if
/// it's empty (e.g. the visitor navigated here directly or refreshed).
/// </summary>
public class OrderState
{
    public OrderDetailDto? LastOrder { get; private set; }

    public void SetLastOrder(OrderDetailDto order) => LastOrder = order;
}
