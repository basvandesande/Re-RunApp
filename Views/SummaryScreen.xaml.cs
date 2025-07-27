namespace Re_RunApp.Views;

using Microsoft.Maui.Controls;
using Re_RunApp.Core;

public partial class SummaryScreen : ContentPage
{
    private PlayerStatistics _statistics;
    private bool _pulseActive = true;
    private string _updatedGpxData;

    public SummaryScreen(PlayerStatistics statistics, string updatedGpxData)
    {
        _statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
        _updatedGpxData = updatedGpxData ?? throw new ArgumentNullException(nameof(updatedGpxData));
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StartStatisticsPulse();

        int avgHeartRate = 0;
        int maxHeartRate = 0;
        if (_statistics.HeartRateSamples.Count > 0)
        {
            avgHeartRate = (int)_statistics.HeartRateSamples.Average();
            maxHeartRate = _statistics.HeartRateSamples.Max();
        }

        TimeSpan avgSpeedMinKm = TimeSpan.FromMinutes(0);
        if (_statistics.CurrentDistanceM.HasValue && _statistics.SecondsElapsed > 0)
        {
            var avgSpeedKmh = (decimal)_statistics.CurrentDistanceM.Value / (decimal)_statistics.SecondsElapsed * 3.6m;

            avgSpeedMinKm=TimeSpan.FromMinutes(60 / (double)avgSpeedKmh);
        }

        AscendLabel.Text = $"{_statistics.TotalInclinationM:N0}";
        AverageHeartRateLabel.Text = $"{avgHeartRate}";
        MaxHeartRateLabel.Text = $"{maxHeartRate}";
        AverageSpeedLabel.Text = $"{avgSpeedMinKm:mm\\:ss}";
        DistanceLabel.Text = $"{_statistics.CurrentDistanceM:N0}";
        DurationLabel.Text = $"{TimeSpan.FromSeconds(_statistics.SecondsElapsed):hh\\:mm\\:ss}";
        TitleLabel.Text = $"#Treadmill - {Runtime.RunSettings?.Name}";
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