namespace Re_RunApp.Views;

using Microsoft.Maui.Controls;

public partial class SummaryScreen : ContentPage
{
    public SummaryScreen()
    {
        InitializeComponent();
    }

    private async void OnExitClicked(object sender, EventArgs e)
    {
        await Navigation.PopToRootAsync();
    }
}