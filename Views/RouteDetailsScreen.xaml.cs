namespace Re_RunApp.Views;

using Microsoft.Maui.Controls;

public partial class RouteDetailsScreen : ContentPage
{
    private readonly string _gpxFilePath;

    public RouteDetailsScreen(string gpxFilePath)
    {
        InitializeComponent();
        _gpxFilePath = gpxFilePath;

    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        // Pass the selected GPX file path to the RouteDetailsScreen
        await Navigation.PushAsync(new ActivityScreen());

    }
}
