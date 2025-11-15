using System.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Re_RunApp;

using Microsoft.Maui.ApplicationModel;
using Re_RunApp.Core;
using Re_RunApp.Views;

public partial class MainPage : ContentPage
{
    private const string FileName = "disclaimer_state.txt";
    private bool _pulseActive = true;
    private readonly IServiceProvider _serviceProvider;
    private bool _folderPickerShown = false;

    public MainPage(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new RouteSelectionScreen());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        StartLogoPulse();
     
        var version = AppInfo.Current.VersionString;
        var build = AppInfo.Current.BuildString;
        var year = (DateTime.Now.Year == 2025) ? "2025" : $"2025-{DateTime.Now.Year}";
        VersionLabel.Text = $"Version {version}  //  © {year} Bas van de Sande  //  RunningApps.eu";

        if (!Runtime.IsUserFolderPersisted())
        {
            if (!_folderPickerShown)
            {
                _folderPickerShown = true;
                // create the modal instance so we can await its completion
                var modal = new Views.FolderSelectModal();
                await Navigation.PushModalAsync(modal, true);

                // Wait until user presses OK in the modal (FolderSelectModal must expose WaitForOkAsync)
                await modal.WaitForOkAsync();
        
                // After modal closed, show native picker
                var picker = _serviceProvider.GetService<IFolderPicker>();
                if (picker != null)
                {
                    var picked = await picker.PickFolderAsync();
                    if (!string.IsNullOrEmpty(picked))
                    {
                        Runtime.SetUserAppFolder(picked);
                    }
                }
            }
        }

        LoadDisclaimerState();

    }

    private async void OnSettingsIconTapped(object sender, EventArgs e)
    {
        // Show the Strava settings screen as a modal popup
        await Navigation.PushModalAsync(new StravaSettingsScreen(), true);
    }

    private async void StartLogoPulse()
    {
        while (_pulseActive)
        {
            await LogoImage.ScaleTo(1.02, 1400, Easing.SinInOut);
            await LogoImage.ScaleTo(0.98, 1400, Easing.SinInOut);
        }
    }

    protected override void OnDisappearing()
    {
        _pulseActive = false;
        base.OnDisappearing();
    }

    private async void OnDisclaimerCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        StartButton.IsEnabled = e.Value;
        SaveDisclaimerState(e.Value);

        if (!e.Value)
            return;

        // Try to create a folder in the user's Documents
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var target = Path.Combine(docs, "Re-Run");

        try
        {
            if (!Directory.Exists(target))
                Directory.CreateDirectory(target);

            // optional marker file
            File.WriteAllText(Path.Combine(target, "re-run-created.txt"), DateTime.UtcNow.ToString("s"));
        }
        catch (UnauthorizedAccessException)
        {
            // Packaged Store app likely blocked — fall back to FolderPicker so user grants access
            var ok = await DisplayAlert(
                "Permission required",
                "I can't create a folder in Documents. Grant access by choosing a folder or enable file access for this app in Windows Settings.",
                "Pick folder",
                "Cancel");

            if (ok)
            {
                var picker = _serviceProvider.GetService<IFolderPicker>();
                if (picker != null)
                {
                    var picked = await picker.PickFolderAsync();
                    if (!string.IsNullOrEmpty(picked))
                    {
                        Runtime.SetUserAppFolderWithToken(picked, null); // or SetUserAppFolder(picked)
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed creating Documents\\Re-Run: {ex.Message}");
        }
    }

    private void SaveDisclaimerState(bool isAccepted)
    {
        try
        {   
            // Write the state to the file
            var filePath = Path.Combine(Runtime.GetAppFolder(), FileName);
            File.WriteAllText(filePath, isAccepted.ToString());
        }
        catch (Exception ex)
        {
            // Handle any errors (e.g., log them)
            Console.WriteLine($"Error saving disclaimer state: {ex.Message}");
        }
    }

    private void LoadDisclaimerState()
    {
        // Set the checkbox and button state
        DisclaimerCheckBox.IsChecked = false;
        DisclaimerCheckBox.IsVisible = true;
        DisclaimerLabel.IsVisible = true;
        StartButton.IsEnabled = false;

        try
        {
            // Get the path to the "rerun" folder
            var filePath = Path.Combine(Runtime.GetAppFolder(), FileName);

            // Check if the file exists
            if (File.Exists(filePath))
            {
                // Read the state from the file
                var content = File.ReadAllText(filePath);
                if (bool.TryParse(content, out var isAccepted))
                {
                    // Set the checkbox and button state
                    DisclaimerCheckBox.IsChecked = isAccepted;
                    // do we need to hide the label and checkbox
                    DisclaimerCheckBox.IsVisible = !isAccepted;
                    DisclaimerLabel.IsVisible = !isAccepted;

                    StartButton.IsEnabled = isAccepted;
                }

            }
        }
        catch (Exception ex)
        {
            // Handle any errors (e.g., log them)
            Console.WriteLine($"Error loading disclaimer state: {ex.Message}");
        }
    }

    private async void OnDisclaimerTapped(object sender, EventArgs e)
    {
        // Open the modal window with the legal disclaimer text
        await Navigation.PushModalAsync(new Views.LegalDisclaimerModal());
    }
}
