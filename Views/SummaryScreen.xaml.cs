namespace Re_RunApp.Views;

using Microsoft.Maui.Controls;
using Re_RunApp.Core;

public partial class SummaryScreen : ContentPage
{
    private PlayerStatistics _statistics;

    public SummaryScreen(PlayerStatistics statistics)
    {
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
        InitializeComponent();
    }

    private async void OnExitClicked(object sender, EventArgs e)
    {
        await Navigation.PopToRootAsync();
    }
}