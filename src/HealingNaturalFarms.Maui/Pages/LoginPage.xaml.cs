using HealingNaturalFarms.Domain.Dtos;
using HealingNaturalFarms.Maui.Services;

namespace HealingNaturalFarms.Maui.Pages;

/// <summary>
/// Doubles as the "Account" tab: shows the sign-in form when signed out,
/// or a small account summary + sign-out button when signed in. Kept as
/// one page (rather than a separate AccountPage) since Shell's TabBar
/// needs exactly one destination for that tab and the two states are
/// mutually exclusive anyway.
/// </summary>
public partial class LoginPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly AuthState _auth;
    private readonly RegionState _region;

    public LoginPage(ApiClient api, AuthState auth, RegionState region)
    {
        InitializeComponent();
        _api = api;
        _auth = auth;
        _region = region;
        _auth.Changed += OnAuthChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshVisibility();
    }

    private void OnAuthChanged() => MainThread.BeginInvokeOnMainThread(RefreshVisibility);

    private void RefreshVisibility()
    {
        AccountSection.IsVisible = _auth.IsAuthenticated;
        LoginSection.IsVisible = !_auth.IsAuthenticated;

        if (_auth.IsAuthenticated)
        {
            AccountNameLabel.Text = $"Hi, {_auth.FirstName}";
            AccountEmailLabel.Text = _auth.Email;
            AccountRegionLabel.Text = $"Preferred storefront: {(_auth.PreferredRegion == Domain.Enums.RegionCode.US ? "United States" : "India")}";
        }
        else
        {
            EmailEntry.Text = "";
            PasswordEntry.Text = "";
            ErrorLabel.IsVisible = false;
        }
    }

    private async void OnSignInClicked(object? sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Enter your email and password.");
            return;
        }

        SignInButton.IsEnabled = false;
        ErrorLabel.IsVisible = false;

        try
        {
            var response = await _api.LoginAsync(new LoginRequest(email, password));
            if (response is null)
            {
                ShowError("Invalid email or password.");
                return;
            }

            await _auth.SignInAsync(response.AccessToken, response.ExpiresAt, response.Email, response.FirstName, response.PreferredRegion);
            await _region.SetRegionAsync(response.PreferredRegion);
        }
        catch (HttpRequestException ex)
        {
            ShowError($"Couldn't sign in: {ex.Message}");
        }
        finally
        {
            SignInButton.IsEnabled = true;
        }
    }

    private async void OnCreateAccountTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RegisterPage));
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        await _auth.SignOutAsync();
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
