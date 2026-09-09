using HealingNaturalFarms.Maui.Pages;

namespace HealingNaturalFarms.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Detail/flow pages are navigated to with parameters
        // (e.g. "productDetail?slug=aloe-vera") rather than being tabs -
        // registering them here is what makes Shell.GoToAsync resolve them.
        Routing.RegisterRoute(nameof(ProductDetailPage), typeof(ProductDetailPage));
        Routing.RegisterRoute(nameof(CheckoutPage), typeof(CheckoutPage));
        Routing.RegisterRoute(nameof(OrderConfirmationPage), typeof(OrderConfirmationPage));
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
    }
}
