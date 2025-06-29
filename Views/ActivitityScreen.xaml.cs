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
    private bool _isFirstSegment=true;
    private bool _pulseActive = true;
    private decimal _nextAnimationDistance = 0;

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
            RouteVideo.Speed = 0.5;
        }
        else
        {
            RouteVideo.ShouldLoopPlayback = true;
            RouteVideo.ShouldAutoPlay = true;
            RouteVideo.Speed = 1;
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

    private void OnTrackChange(decimal totalDistanceInMeters, decimal nextSegmentInMeters)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _isFirstSegment = (totalDistanceInMeters < 10);

            if (!_isFirstSegment)  StopPulseAnimation();
       
            PlotView.Model = _graphPlotter.RenderDistanceOverlay(totalDistanceInMeters, nextSegmentInMeters);
        });
    }

    

    private void OnStatisticsUpdate(PlayerStatistics stats)
    {
        // Ensure UI updates are on the main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DistanceLabel.Text = $"{stats.CurrentDistanceM:N0} / {_gpxProcessor.TotalDistanceInMeters:N0}";
            SegmentLabel.Text = $"{stats.SegmentRemainingM:N0}";
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

            // do we need to show the animation?
            if (stats.CurrentDistanceM >= _nextAnimationDistance)
            {
                int? repeat = (_nextAnimationDistance >= (_gpxProcessor.TotalDistanceInMeters / 4 * 3)) ? 0 : 3;
                StartPulseAnimation(repeat); 
            }
        });
    }

    private void UpdateRouteVideoSpeed()
    {
        if (_hasVideo && RouteVideo.Duration.TotalSeconds > 0)
        {
            double secondsToGo = CalculateRemainingSeconds();
            double remainingVideoSeconds = RouteVideo.Duration.TotalSeconds - RouteVideo.Position.TotalSeconds;

            // If the video is not playing, we set the speed to 0 (same applies for the first segment)
            if (!_isFirstSegment) 
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
        RouteVideo.Play();
        RouteVideo.Speed = 0.5;
        await _player.StartAsync();

        StartPulseAnimation();
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

    private void StopPulseAnimation()
    {
        _pulseActive = false;
        StartPulseImage.IsVisible = false;

        // ensure we have the next animation waiting in the background
        decimal totalDistance = _gpxProcessor.TotalDistanceInMeters;

        // set the correct image
        if (_nextAnimationDistance <= totalDistance / 4)
            StartPulseImage.Source = "checkpoint.png";
        else if (_nextAnimationDistance <= totalDistance / 2)
            StartPulseImage.Source = "halfway.png";
        else if (_nextAnimationDistance <= (3 * totalDistance) / 4)
            StartPulseImage.Source = "almostthere.png";
        else
            StartPulseImage.Source = "finish.png";
    }



    private async void StartPulseAnimation(int? repeatCount = null)
    {
        _pulseActive = true;
        StartPulseImage.IsVisible = true;

        // Calculate the next animation distance based on the current distance and the total distance
        // i want to animate at 25 / 50/ 75 / 100 percent of the total distance
        decimal totalDistance = _gpxProcessor.TotalDistanceInMeters;
        decimal currentDistance = _playerStatistics.CurrentDistanceM ?? 0;
        decimal nextSegmentLength = totalDistance / 4; // 25% of the total distance

        if (currentDistance + nextSegmentLength > totalDistance)
            nextSegmentLength = totalDistance - currentDistance - 20; // give some extra length :)

        _nextAnimationDistance = currentDistance + nextSegmentLength;
        int repeats = 0;
        while (_pulseActive && (repeatCount == null || repeats < repeatCount))
        {
            await StartPulseImage.ScaleTo(1.10, 800, Easing.SinInOut);
            await StartPulseImage.ScaleTo(0.90, 800, Easing.SinInOut);
            repeats++;
        }

        StopPulseAnimation();
    }
    

}
