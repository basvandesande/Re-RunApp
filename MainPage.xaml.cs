namespace Re_RunApp;

using Microsoft.Maui.ApplicationModel;
using Re_RunApp.Views;

public partial class MainPage : ContentPage
{
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
        StartLogoPulse();

        var version = AppInfo.Current.VersionString;
        var build = AppInfo.Current.BuildString;
        var year = (DateTime.Now.Year==2025)? "2025" : $"2025-{DateTime.Now.Year}";
        VersionLabel.Text = $"© Re-RunApp  v{version} (build {build})   - © {year} Bas van de Sande - azurecodingarchitect.com";


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
}
