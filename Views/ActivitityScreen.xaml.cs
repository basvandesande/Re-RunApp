namespace Re_RunApp.Views;

using Microsoft.Maui.Controls;

public partial class ActivityScreen : ContentPage
{
    public ActivityScreen()
    {
        InitializeComponent();
    }

    private async void OnFinishClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SummaryScreen());
    }
}
