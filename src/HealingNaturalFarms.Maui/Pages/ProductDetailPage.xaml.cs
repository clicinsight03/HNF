using HealingNaturalFarms.Domain.Dtos;
using HealingNaturalFarms.Domain.Enums;
using HealingNaturalFarms.Maui.Converters;
using HealingNaturalFarms.Maui.Services;

namespace HealingNaturalFarms.Maui.Pages;

/// <summary>
/// Navigated to as "ProductDetailPage?slug=..." from ShopPage - Shell
/// hands the query parameter to this page via QueryProperty below,
/// which fires OnSlugSet as soon as it's set (before OnAppearing, so the
/// load kicks off immediately rather than waiting for the page to show).
/// </summary>
[QueryProperty(nameof(Slug), "slug")]
public partial class ProductDetailPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly RegionState _region;
    private readonly CartState _cart;

    private ProductDetailDto? _product;
    private int _qty = 1;
    private string _slug = "";

    public string Slug
    {
        get => _slug;
        set
        {
            _slug = Uri.UnescapeDataString(value ?? "");
            _ = LoadAsync();
        }
    }

    public ProductDetailPage(ApiClient api, RegionState region, CartState cart)
    {
        InitializeComponent();
        _api = api;
        _region = region;
        _cart = cart;
    }

    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(_slug)) return;

        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;
        ContentScroll.IsVisible = false;
        _qty = 1;

        _product = await _api.GetProductAsync(_slug, _region.Current);

        LoadingIndicator.IsRunning = false;
        LoadingIndicator.IsVisible = false;

        if (_product is null)
        {
            await DisplayAlert("Not available", "This item isn't available in your current storefront region.", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        ContentScroll.IsVisible = true;
        Render(_product);
    }

    private void Render(ProductDetailDto p)
    {
        Title = p.Name;
        MainImage.Source = p.ImageUrls.Count > 0 ? p.ImageUrls[0] : null;

        if (p.ImageUrls.Count > 1)
        {
            ThumbnailsView.IsVisible = true;
            ThumbnailsView.ItemsSource = p.ImageUrls;
        }
        else
        {
            ThumbnailsView.IsVisible = false;
        }

        NameLabel.Text = p.Name;
        SkuLabel.Text = $"SKU: {p.Sku}";
        PriceLabel.Text = MoneyConverter.Format(p.Price, p.Currency);

        if (!string.IsNullOrWhiteSpace(p.ShortDescription))
        {
            ShortDescriptionLabel.Text = p.ShortDescription;
            ShortDescriptionLabel.IsVisible = true;
        }

        RenderAttributes(p);

        QtyLabel.Text = "1";
        AddedLabel.IsVisible = false;

        var inStock = p.InStock;
        PurchaseStack.IsVisible = inStock;
        OutOfStockLabel.IsVisible = !inStock;

        if (!string.IsNullOrWhiteSpace(p.Description))
        {
            DescriptionHeaderLabel.IsVisible = true;
            DescriptionLabel.Text = p.Description;
            DescriptionLabel.IsVisible = true;
        }
        else
        {
            DescriptionHeaderLabel.IsVisible = false;
            DescriptionLabel.IsVisible = false;
        }
    }

    private void RenderAttributes(ProductDetailDto p)
    {
        AttributesStack.Children.Clear();

        var mutedColor = (Color)Application.Current!.Resources["MutedText"];

        void AddRow(string label, string value)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            var labelView = new Label { Text = label, TextColor = mutedColor };
            var valueView = new Label { Text = value };
            Grid.SetColumn(labelView, 0);
            Grid.SetColumn(valueView, 1);
            row.Children.Add(labelView);
            row.Children.Add(valueView);

            AttributesStack.Children.Add(row);
        }

        if (p.ProductType == ProductType.Plant)
        {
            if (p.SunlightNeeds is not null) AddRow("Sunlight", FormatEnum(p.SunlightNeeds.Value.ToString()));
            if (!string.IsNullOrWhiteSpace(p.WateringFrequency)) AddRow("Watering", p.WateringFrequency);
            if (p.PotSizeCm is not null) AddRow("Pot size", $"{p.PotSizeCm} cm");
            if (p.MatureHeightCm is not null) AddRow("Mature height", $"{p.MatureHeightCm} cm");
            if (p.IsIndoor is not null) AddRow("Setting", p.IsIndoor.Value ? "Indoor" : "Outdoor");
            if (p.IsPetSafe is not null) AddRow("Pet safe", p.IsPetSafe.Value ? "Yes" : "No");
        }
        else
        {
            if (p.UnitOfSale is not null) AddRow("Sold", FormatEnum(p.UnitOfSale.Value.ToString()));
            if (p.IsOrganic is not null) AddRow("Organic", p.IsOrganic.Value ? "Yes" : "No");
            if (p.IsSeasonal is not null) AddRow("Seasonal", p.IsSeasonal.Value ? "Yes" : "No");
        }
    }

    private static string FormatEnum(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", " $1");

    private void OnThumbnailTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TapGestureRecognizer { CommandParameter: string url })
        {
            MainImage.Source = url;
        }
    }

    private void OnDecrementQty(object? sender, EventArgs e)
    {
        _qty = Math.Max(1, _qty - 1);
        QtyLabel.Text = _qty.ToString();
    }

    private void OnIncrementQty(object? sender, EventArgs e)
    {
        _qty++;
        QtyLabel.Text = _qty.ToString();
    }

    private async void OnAddToCartClicked(object? sender, EventArgs e)
    {
        if (_product is null) return;

        AddToCartButton.IsEnabled = false;
        try
        {
            await _cart.AddAsync(_product.Id, _qty);
            AddedLabel.IsVisible = true;
        }
        catch (HttpRequestException ex)
        {
            await DisplayAlert("Couldn't add to cart", ex.Message, "OK");
        }
        finally
        {
            AddToCartButton.IsEnabled = true;
        }
    }
}
