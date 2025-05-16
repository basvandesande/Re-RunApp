namespace Re_RunApp.Views;

using Microsoft.Maui.Controls;
using Re_RunApp.Core;

public partial class ActivityScreen : ContentPage
{
    private string _gpxFilePath;

    public ActivityScreen(string gpxFilePath)
    {
        InitializeComponent();
        _gpxFilePath = gpxFilePath;
    }

    private async void OnFinishClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SummaryScreen());
    }
}
