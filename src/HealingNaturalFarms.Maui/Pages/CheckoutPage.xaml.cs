using System.Globalization;
using HealingNaturalFarms.Domain.Dtos;
using HealingNaturalFarms.Domain.Enums;
using HealingNaturalFarms.Maui.Converters;
using HealingNaturalFarms.Maui.Services;

namespace HealingNaturalFarms.Maui.Pages;

public partial class CheckoutPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly RegionState _region;
    private readonly CartState _cart;
    private readonly AuthState _auth;
    private readonly OrderState _orderState;

    private List<PaymentProvider> _availableProviders = [];
    private CreateCheckoutResponse? _checkout;

    public CheckoutPage(ApiClient api, RegionState region, CartState cart, AuthState auth, OrderState orderState)
    {
        InitializeComponent();
        _api = api;
        _region = region;
        _cart = cart;
        _auth = auth;
        _orderState = orderState;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        AddressSection.IsVisible = true;
        PaymentSection.IsVisible = false;

        EmailEntry.Text = _auth.Email ?? EmailEntry.Text;
        FullNameEntry.Text = _auth.FirstName ?? FullNameEntry.Text;
        if (string.IsNullOrWhiteSpace(CountryEntry.Text))
        {
            CountryEntry.Text = _region.Current == RegionCode.US ? "United States" : "India";
        }

        _availableProviders = _region.Current == RegionCode.US
            ? [PaymentProvider.Stripe, PaymentProvider.PayPal]
            : [PaymentProvider.Razorpay];

        ProviderPicker.ItemsSource = _availableProviders.Select(ProviderLabel).ToList();
        ProviderPicker.SelectedIndex = 0;
    }

    private static string ProviderLabel(PaymentProvider provider) => provider switch
    {
        PaymentProvider.Stripe => "Card (Stripe)",
        PaymentProvider.PayPal => "PayPal",
        PaymentProvider.Razorpay => "Cards / UPI / Netbanking (Razorpay)",
        _ => provider.ToString()
    };

    private async void OnContinueClicked(object? sender, EventArgs e)
    {
        if (_cart.Cart is null || _cart.Cart.Items.Count == 0)
        {
            await DisplayAlert("Cart is empty", "Add something to your cart before checking out.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(FullNameEntry.Text) || string.IsNullOrWhiteSpace(EmailEntry.Text)
            || string.IsNullOrWhiteSpace(Line1Entry.Text) || string.IsNullOrWhiteSpace(CityEntry.Text)
            || string.IsNullOrWhiteSpace(StateEntry.Text) || string.IsNullOrWhiteSpace(PostalCodeEntry.Text)
            || string.IsNullOrWhiteSpace(CountryEntry.Text))
        {
            ShowAddressError("Please fill in every required field.");
            return;
        }

        if (ProviderPicker.SelectedIndex < 0)
        {
            ShowAddressError("Choose a payment method.");
            return;
        }

        var provider = _availableProviders[ProviderPicker.SelectedIndex];

        ContinueButton.IsEnabled = false;
        SetBusy(true);
        AddressErrorLabel.IsVisible = false;

        try
        {
            var request = new CreateCheckoutRequest(
                _cart.Cart.CartId,
                provider,
                new AddressDto(null, FullNameEntry.Text, Line1Entry.Text, Line2Entry.Text, CityEntry.Text, StateEntry.Text, PostalCodeEntry.Text, CountryEntry.Text, PhoneEntry.Text),
                EmailEntry.Text,
                PhoneEntry.Text);

            _checkout = await _api.CreateCheckoutAsync(request);
            if (_checkout is null)
            {
                ShowAddressError("Could not start checkout. Please try again.");
                return;
            }

            await ShowPaymentStepAsync(_checkout);
        }
        catch (HttpRequestException ex)
        {
            ShowAddressError($"Checkout failed: {ex.Message}");
        }
        finally
        {
            ContinueButton.IsEnabled = true;
            SetBusy(false);
        }
    }

    private async Task ShowPaymentStepAsync(CreateCheckoutResponse checkout)
    {
        var template = await FileSystem.OpenAppPackageFileAsync("checkout.html");
        using var reader = new StreamReader(template);
        var html = await reader.ReadToEndAsync();

        var amountSmallestUnit = (long)Math.Round(checkout.Amount * 100, MidpointRounding.AwayFromZero);

        html = html
            .Replace("__PROVIDER__", checkout.Provider.ToString())
            .Replace("__PUBLIC_KEY__", checkout.ProviderPublicKey ?? "")
            .Replace("__CLIENT_SECRET__", checkout.ClientSecret ?? "")
            .Replace("__PROVIDER_ORDER_ID__", checkout.ProviderOrderId ?? "")
            .Replace("__CURRENCY__", checkout.Currency.ToString())
            .Replace("__AMOUNT_SMALLEST_UNIT__", amountSmallestUnit.ToString(CultureInfo.InvariantCulture))
            .Replace("__AMOUNT_DISPLAY__", MoneyConverter.Format(checkout.Amount, checkout.Currency))
            .Replace("__ORDER_NUMBER__", checkout.OrderNumber);

        PaymentWebView.Source = new HtmlWebViewSource { Html = html, BaseUrl = "https://checkout.local/" };
        AddressSection.IsVisible = false;
        PaymentSection.IsVisible = true;
        PaymentErrorLabel.IsVisible = false;
    }

    /// <summary>
    /// checkout.html can't call back into C# directly (that needs
    /// HybridWebView, a different control) - instead it navigates to
    /// "app://payment-result?..." when it has an outcome, which this
    /// intercepts and cancels before the WebView ever tries to actually
    /// load it. See checkout.html's reportResult() for the JS side.
    /// </summary>
    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!e.Url.StartsWith("app://payment-result", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;
        var fields = ParseQuery(e.Url);

        var success = fields.TryGetValue("success", out var successRaw) && bool.TryParse(successRaw, out var s) && s;
        if (!success)
        {
            var message = fields.GetValueOrDefault("message", "Payment failed.");
            PaymentErrorLabel.Text = message;
            PaymentErrorLabel.IsVisible = true;
            return;
        }

        if (_checkout is null) return;

        SetBusy(true);
        try
        {
            var paymentReference = fields.GetValueOrDefault("paymentReference", "");
            var signature = fields.TryGetValue("signature", out var sig) ? sig : null;

            var order = await _api.ConfirmPaymentAsync(new ConfirmPaymentRequest(_checkout.OrderId, _checkout.Provider, paymentReference, signature));
            if (order is null)
            {
                PaymentErrorLabel.Text = "We couldn't confirm your payment. Please contact support with your order number.";
                PaymentErrorLabel.IsVisible = true;
                return;
            }

            _orderState.SetLastOrder(order);
            await _cart.ClearAsync();
            await Shell.Current.GoToAsync(nameof(OrderConfirmationPage));
        }
        catch (HttpRequestException ex)
        {
            PaymentErrorLabel.Text = $"We couldn't confirm your payment: {ex.Message}";
            PaymentErrorLabel.IsVisible = true;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static Dictionary<string, string> ParseQuery(string url)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var queryStart = url.IndexOf('?');
        if (queryStart < 0 || queryStart == url.Length - 1) return result;

        var query = url[(queryStart + 1)..];
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            result[key] = value;
        }

        return result;
    }

    private void ShowAddressError(string message)
    {
        AddressErrorLabel.Text = message;
        AddressErrorLabel.IsVisible = true;
    }

    private void SetBusy(bool busy)
    {
        BusyIndicator.IsRunning = busy;
        BusyIndicator.IsVisible = busy;
    }
}
