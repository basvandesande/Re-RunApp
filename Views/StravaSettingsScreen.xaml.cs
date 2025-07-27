using Re_RunApp.Core;

namespace Re_RunApp.Views;

public partial class StravaSettingsScreen : ContentPage
{
    public StravaSettingsScreen()
    {
        InitializeComponent();

        // Load existing settings
        var settings = Runtime.StravaSettings;
        if (settings != null)
        {
            ClientIdEntry.Text = settings.ClientId;
            ClientSecretEntry.Text = settings.ClientSecret;
            RedirectUrlEntry.Text = settings.RedirectUrl;
        }
    }

    private async void OnSaveButtonClicked(object sender, EventArgs e)
    {
        // Save the settings
        var settings = new StravaSettings
        {
            ClientId = ClientIdEntry.Text ?? string.Empty,
            ClientSecret = ClientSecretEntry.Text ?? string.Empty,
            RedirectUrl = RedirectUrlEntry.Text ?? string.Empty
        };

        await ConfigurationManager.SaveStravaSettingsAsync(settings);

        // Update the runtime settings
        Runtime.StravaSettings = settings;

        // Close the modal popup
        await Navigation.PopModalAsync();
    }
}