namespace Re_RunApp.Views;

using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using OxyPlot.Maui.Skia;
using Re_RunApp.Core;

public partial class ActivityScreen : ContentPage
{
    private string _gpxFilePath;
    private GpxProcessor _gpxProcessor = new();
    private ITreadmill _treadmill;
    private IHeartRate _heartRate;
    private GraphPlotter _graphPlotter = new();
    private Player _player;
    private PlayerStatistics _playerStatistics = new();
    private decimal? _actualSpeed = 0;
    private bool _hasVideo = false;

    public ActivityScreen(string gpxFilePath, bool simulate=false)
    {
        InitializeComponent();

        _gpxFilePath = gpxFilePath;
        _gpxProcessor.LoadGpxData(_gpxFilePath);
        _gpxProcessor.GetRun();

        _treadmill = (!simulate)? Runtime.Treadmill: Runtime.TreadmillSimulator;
        _heartRate = (!simulate) ? Runtime.HeartRate : Runtime.HeartRateSimulator;

        if (simulate) _heartRate.Enabled = true; // Enable heart rate simulation if in simulation mode

        _player = new Player(_gpxProcessor, _treadmill, _heartRate, Runtime.SpeedSettings);
        _player.OnStatisticsUpdate += OnStatisticsUpdate;
        _player.OnTrackChange += OnTrackChange; 
        _player.OnTrackReady += OnTrackReady;
     
        this.Loaded += ActivityScreen_Loaded;
        this.Unloaded += ActivityScreen_Unloaded;
    }

    private void ActivityScreen_Loaded(object? sender, EventArgs e)
    {
        PlotView.Model = _graphPlotter.PlotGraph(_gpxProcessor, false);

        this.Title = Path.GetFileNameWithoutExtension(_gpxFilePath);

        string videoPath = Path.ChangeExtension(_gpxFilePath, ".mp4");
        _hasVideo = File.Exists(videoPath);

        if (_hasVideo)
        {
            RouteVideo.Source = MediaSource.FromFile(videoPath);
            RouteVideo.ShouldLoopPlayback = false;
            RouteVideo.ShouldAutoPlay = false;
            RouteVideo.Pause();
        }
        else
        {
            RouteVideo.ShouldLoopPlayback = true;
            RouteVideo.ShouldAutoPlay = true;
            RouteVideo.Play();
        }
        RouteVideo.IsVisible = true;
    }


    private void ActivityScreen_Unloaded(object? sender, EventArgs e)
    {
        _player?.Dispose();
        _treadmill.Disconnect();
        _heartRate.Disconnect();
        PlotView.Model = null;
    }


    private void OnTrackReady(PlayerStatistics statistics)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnStatisticsUpdate(statistics);

            StartButton.IsVisible = false;
            StopButton.IsVisible = false;
            FinishButton.IsVisible = true;
        });
    }

    private void OnTrackChange(decimal totalDistanceInMeters)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PlotView.Model = _graphPlotter.RenderDistanceOverlay(totalDistanceInMeters);
        });
    }

    

    private void OnStatisticsUpdate(PlayerStatistics stats)
    {
        // Ensure UI updates are on the main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DistanceLabel.Text = $"{stats.CurrentDistanceM:N0}";
            TimeLabel.Text = $"{TimeSpan.FromSeconds(stats.SecondsElapsed):hh\\:mm\\:ss}";
            SpeedLabel.Text = $"{stats.CurrentSpeedMinKM:mm\\:ss}";
            //InclinationLabel.Text = $"{stats.SegmentIncrementPercentage:N1}";
            HeartrateLabel.Text = $"{stats.CurrentHeartRate}";
            //TotalClimbedLabel.Text = $"{stats.TotalInclinationM:N0}";
            //TotalDescendedLabel.Text = $"{stats.TotalDeclinationM:N0}";
            //SegmentRemainingLabel.Text = $"{stats.SegmentRemainingM:N0}";

            // store the statistics for later use
            _playerStatistics = stats;

            // change video speed if needed.... otherwise use the actual speed
            if (_actualSpeed != stats.CurrentSpeedKMH)
            {
                _actualSpeed = stats.CurrentSpeedKMH;
                UpdateRouteVideoSpeed();
            }
        });
    }

    private void UpdateRouteVideoSpeed()
    {
        if (_hasVideo && RouteVideo.Duration.TotalSeconds > 0)
        {
            double secondsToGo = CalculateRemainingSeconds();
            double remainingVideoSeconds = RouteVideo.Duration.TotalSeconds - RouteVideo.Position.TotalSeconds;
           
            RouteVideo.Speed = (secondsToGo > 0) ? (remainingVideoSeconds / secondsToGo) : 0;
        }
    }

    private double CalculateRemainingSeconds()
    {
        decimal? totalDistanceM = _gpxProcessor.TotalDistanceInMeters;
        decimal? distanceCoveredM = _playerStatistics.CurrentDistanceM;
        decimal? currentSpeedKmh = _playerStatistics.CurrentSpeedKMH;

        // Convert speed from km/h to m/s
        double currentSpeedMps = currentSpeedKmh.HasValue ? (double)currentSpeedKmh.Value / 3.6 : 0;

        // Remaining distance
        double remainingDistanceM = (double)((totalDistanceM ?? 0) - (distanceCoveredM ?? 0));

        // Remaining time in seconds
        double remainingTimeSeconds = (currentSpeedMps > 0)
                                        ? Math.Max(0, remainingDistanceM / currentSpeedMps)
                                        : double.PositiveInfinity;

        return remainingTimeSeconds;
    }


    private async void OnStartClicked(object sender, EventArgs e)
    {   
        // swap buttons
        StartButton.IsVisible = false;
        StopButton.IsVisible = true;
        FinishButton.IsVisible = false;

        // ensure the video starts playing (stand still, waiting for motion on treadmill)
        RouteVideo.Speed = 0;
        RouteVideo.Play();
        await _player.StartAsync();
    }


    private async void OnFinishClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SummaryScreen(_playerStatistics));
    }

    private async void OnStopClicked(object sender, EventArgs e)
    {
        StartButton.IsVisible = false;
        StopButton.IsVisible = false;
        FinishButton.IsVisible = true;
        RouteVideo.Pause();
        await _player.StopAsync();
    }
    

}
