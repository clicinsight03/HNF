using HealingNaturalFarms.Domain.Dtos;
using HealingNaturalFarms.Domain.Enums;
using HealingNaturalFarms.Maui.Services;

namespace HealingNaturalFarms.Maui.Pages;

public partial class RegisterPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly AuthState _auth;
    private readonly RegionState _region;

    public RegisterPage(ApiClient api, AuthState auth, RegionState region)
    {
        InitializeComponent();
        _api = api;
        _auth = auth;
        _region = region;
        RegionPicker.SelectedIndex = region.Current == RegionCode.US ? 0 : 1;
    }

    private async void OnCreateAccountClicked(object? sender, EventArgs e)
    {
        var firstName = FirstNameEntry.Text?.Trim() ?? "";
        var lastName = LastNameEntry.Text?.Trim() ?? "";
        var email = EmailEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";
        var preferredRegion = RegionPicker.SelectedIndex == 1 ? RegionCode.IN : RegionCode.US;

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName)
            || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            ShowError("Please fill in every field.");
            return;
        }

        CreateAccountButton.IsEnabled = false;
        ErrorLabel.IsVisible = false;

        try
        {
            var response = await _api.RegisterAsync(new RegisterRequest(email, password, firstName, lastName, preferredRegion));
            if (response is null)
            {
                ShowError("Couldn't create your account. The email may already be registered, or the password may not meet requirements.");
                return;
            }

            await _auth.SignInAsync(response.AccessToken, response.ExpiresAt, response.Email, response.FirstName, response.PreferredRegion);
            await _region.SetRegionAsync(response.PreferredRegion);
            await Shell.Current.GoToAsync("..");
        }
        catch (HttpRequestException ex)
        {
            ShowError($"Couldn't create your account: {ex.Message}");
        }
        finally
        {
            CreateAccountButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
