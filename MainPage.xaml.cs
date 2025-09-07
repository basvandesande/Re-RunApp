using System.IO;

namespace Re_RunApp;

using Microsoft.Maui.ApplicationModel;
using Re_RunApp.Core;
using Re_RunApp.Views;

public partial class MainPage : ContentPage
{
    private const string FileName = "disclaimer_state.txt";
    private bool _pulseActive = true;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new RouteSelectionScreen());
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadDisclaimerState();

        StartLogoPulse();

        var version = AppInfo.Current.VersionString;
        var build = AppInfo.Current.BuildString;
        var year = (DateTime.Now.Year == 2025) ? "2025" : $"2025-{DateTime.Now.Year}";
        VersionLabel.Text = $"Version {version}  //  © {year} Bas van de Sande  //  RunningApps.eu";
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

    private void OnDisclaimerCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        // Enable the Start button only if the checkbox is checked
        StartButton.IsEnabled = e.Value;

        // Save the state to a file
        SaveDisclaimerState(e.Value);
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
                    StartButton.IsEnabled = isAccepted;
                }

                // do we need to hide the label and checkbox
                DisclaimerCheckBox.IsVisible = !isAccepted;
                DisclaimerLabel.IsVisible = !isAccepted;

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
