using System.Collections.ObjectModel;
using HealingNaturalFarms.Domain.Dtos;
using HealingNaturalFarms.Domain.Enums;
using HealingNaturalFarms.Maui.Services;

namespace HealingNaturalFarms.Maui.Pages;

public partial class ShopPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly RegionState _region;
    private readonly CartState _cart;

    private readonly ObservableCollection<ProductListItemDto> _products = new();
    private ProductType _productType = ProductType.Plant;
    private IReadOnlyList<CategoryDto> _categories = [];
    private int? _selectedCategoryId;
    private string _search = "";
    private int _page = 1;
    private int _totalCount; // server defaults to a page size of 24 (see CatalogController.GetProducts) - not repeated here since the client never overrides it
    private CancellationTokenSource? _searchDebounce;
    private bool _suppressPickerEvents;

    public ShopPage(ApiClient api, RegionState region, CartState cart)
    {
        InitializeComponent();
        _api = api;
        _region = region;
        _cart = cart;

        ProductsView.ItemsSource = _products;
        RegionPicker.SelectedIndex = _region.Current == RegionCode.US ? 0 : 1;

        _cart.Changed += OnCartChanged;
        _region.Changed += OnRegionStateChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UpdateCartSummary();
        if (_categories.Count == 0)
        {
            await LoadCategoriesAsync();
        }
        if (_products.Count == 0)
        {
            await LoadProductsAsync(resetPage: true);
        }
    }

    // ---- Region / department / category / search ------------------------

    private async void OnRegionChanged(object? sender, EventArgs e)
    {
        if (_suppressPickerEvents) return;
        var region = RegionPicker.SelectedIndex == 1 ? RegionCode.IN : RegionCode.US;
        await _region.SetRegionAsync(region);
        // OnRegionStateChanged (subscribed above) reloads the catalog.
    }

    private async void OnRegionStateChanged()
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            _suppressPickerEvents = true;
            RegionPicker.SelectedIndex = _region.Current == RegionCode.US ? 0 : 1;
            _suppressPickerEvents = false;
            await LoadCategoriesAsync();
            await LoadProductsAsync(resetPage: true);
        });
    }

    private async void OnPlantTabClicked(object? sender, EventArgs e) => await SwitchDepartmentAsync(ProductType.Plant);
    private async void OnProduceTabClicked(object? sender, EventArgs e) => await SwitchDepartmentAsync(ProductType.Produce);

    private async Task SwitchDepartmentAsync(ProductType type)
    {
        if (_productType == type) return;
        _productType = type;
        // TryGetResource walks page -> Application, unlike indexing
        // Resources directly (which only sees this page's own
        // dictionary) - PrimaryButton/SecondaryButton are defined in
        // App.xaml's merged Styles.xaml, not on this page.
        this.TryGetResource(type == ProductType.Plant ? "PrimaryButton" : "SecondaryButton", out var plantStyle);
        this.TryGetResource(type == ProductType.Produce ? "PrimaryButton" : "SecondaryButton", out var produceStyle);
        PlantButton.Style = (Style)plantStyle;
        ProduceButton.Style = (Style)produceStyle;
        _selectedCategoryId = null;
        await LoadCategoriesAsync();
        await LoadProductsAsync(resetPage: true);
    }

    private async Task LoadCategoriesAsync()
    {
        _categories = await _api.GetCategoriesAsync(_productType) ?? [];
        _suppressPickerEvents = true;
        CategoryPicker.ItemsSource = new List<string> { "All categories" }.Concat(_categories.Select(c => c.Name)).ToList();
        CategoryPicker.SelectedIndex = 0;
        _suppressPickerEvents = false;
    }

    private async void OnCategoryChanged(object? sender, EventArgs e)
    {
        if (_suppressPickerEvents) return;
        var index = CategoryPicker.SelectedIndex;
        _selectedCategoryId = index <= 0 ? null : _categories[index - 1].Id;
        await LoadProductsAsync(resetPage: true);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _search = e.NewTextValue ?? "";

        _searchDebounce?.Cancel();
        var cts = new CancellationTokenSource();
        _searchDebounce = cts;
        _ = DebouncedSearchAsync(cts.Token);
    }

    private async Task DebouncedSearchAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(400, ct);
            if (!ct.IsCancellationRequested)
            {
                await LoadProductsAsync(resetPage: true);
            }
        }
        catch (TaskCanceledException)
        {
            // superseded by a newer keystroke
        }
    }

    // ---- Loading ---------------------------------------------------------

    private async Task LoadProductsAsync(bool resetPage)
    {
        if (resetPage) _page = 1;

        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;

        try
        {
            var result = await _api.GetProductsAsync(_region.Current, _productType, _selectedCategoryId, _search, _page);
            if (resetPage) _products.Clear();

            if (result is not null)
            {
                foreach (var item in result.Items)
                {
                    _products.Add(item);
                }
                _totalCount = result.TotalCount;
            }

            LoadMoreButton.IsVisible = _products.Count < _totalCount;
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
            ProductsRefreshView.IsRefreshing = false;
        }
    }

    private async void OnLoadMoreClicked(object? sender, EventArgs e)
    {
        _page++;
        await LoadProductsAsync(resetPage: false);
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadProductsAsync(resetPage: true);
    }

    // ---- Navigation / cart -------------------------------------------------

    private async void OnProductTapped(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string slug })
        {
            await Shell.Current.GoToAsync($"{nameof(ProductDetailPage)}?slug={Uri.EscapeDataString(slug)}");
        }
    }

    private async void OnProductFrameTapped(object? sender, TappedEventArgs e)
    {
        if (sender is TapGestureRecognizer { CommandParameter: string slug })
        {
            await Shell.Current.GoToAsync($"{nameof(ProductDetailPage)}?slug={Uri.EscapeDataString(slug)}");
        }
    }

    private async void OnCartTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//cart");
    }

    private void OnCartChanged() => MainThread.BeginInvokeOnMainThread(UpdateCartSummary);

    private void UpdateCartSummary()
    {
        CartSummaryLabel.Text = $"Cart ({_cart.ItemCount})";
    }
}
