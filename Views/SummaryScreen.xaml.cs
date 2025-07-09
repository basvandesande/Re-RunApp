namespace Re_RunApp.Views;

using Microsoft.Maui.Controls;
using Re_RunApp.Core;

public partial class SummaryScreen : ContentPage
{
    private PlayerStatistics _statistics;
    private bool _pulseActive = true;

    public SummaryScreen(PlayerStatistics statistics)
    {
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartStatisticsPulse();

        AscendLabel.Text = $"{_statistics.TotalInclinationM:N0}";
        AverageHeartRateLabel.Text = $"{_statistics.CurrentHeartRate}";
        AverageSpeedLabel.Text = $"{_statistics.CurrentSpeedMinKM:mm\\:ss}";
        DistanceLabel.Text = $"{_statistics.CurrentDistanceM:N0}";
        DurationLabel.Text = $"{TimeSpan.FromSeconds(_statistics.SecondsElapsed):hh\\:mm\\:ss}";
        TitleLabel.Text = Runtime.RunSettings?.Name;

    }

    protected override void OnDisappearing()
    {
        _pulseActive = false;
        base.OnDisappearing();
    }

    private async void OnExitClicked(object sender, EventArgs e)
    {
        await Navigation.PopToRootAsync();
    }

    private async void StartStatisticsPulse()
    {
        while (_pulseActive)
        {
            await StatisticsImage.ScaleTo(1.02, 1400, Easing.SinInOut);
            await StatisticsImage.ScaleTo(0.98, 1400, Easing.SinInOut);
        }
    }

}