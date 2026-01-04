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
    private bool _isFirstSegment = true;
    private bool _pulseActive = false;
    private readonly decimal[] _milestones = { 0.25m, 0.5m, 0.75m, 0.993m };
    private int _nextMilestoneIndex = 0;
    private bool _simulate = false;

    public ActivityScreen(string gpxFilePath, bool simulate = false, decimal skipMeters = 0)
    {
        InitializeComponent();

        _gpxFilePath = gpxFilePath;
        _gpxProcessor.LoadGpxData(_gpxFilePath);
        _gpxProcessor.GetRun(skipMeters);

        _treadmill = (!simulate) ? Runtime.Treadmill : Runtime.TreadmillSimulator;
        _heartRate = (!simulate) ? Runtime.HeartRate : Runtime.HeartRateSimulator;
        _simulate = simulate;

        if (simulate) _heartRate.Enabled = true; // Enable heart rate simulation if in simulation mode

        // Ensure RunSettings is not null before passing to Player
        var runSettings = Runtime.RunSettings ?? new RunSettings();
        _player = new Player(_gpxProcessor, _treadmill, _heartRate, runSettings);
        _player.OnStatisticsUpdate += OnStatisticsUpdate;
        _player.OnTrackChange += OnTrackChange;
        _player.OnTrackReady += OnTrackReady;

        this.Loaded += ActivityScreen_Loaded;
        this.Unloaded += ActivityScreen_Unloaded;

        RouteVideo.MediaOpened += OnMediaOpened;
    }

    private void ActivityScreen_Loaded(object? sender, EventArgs e)
    { }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        PlotView.Model = _graphPlotter.PlotGraph(_gpxProcessor, false);

        // Removed: this.Title = _gpxProcessor?.Gpx?.trk?.name ?? string.Empty;
        // Title is now hidden via Shell.NavBarIsVisible="False" in XAML

        string videoPath = Path.ChangeExtension(_gpxFilePath, ".mp4");
        _hasVideo = File.Exists(videoPath);

        if (_hasVideo)
        {
            // Subscribe to MediaOpened before setting the source
            RouteVideo.MediaOpened -= OnMediaOpened;
            RouteVideo.MediaOpened += OnMediaOpened;

            RouteVideo.Source = MediaSource.FromFile(videoPath);
            RouteVideo.ShouldLoopPlayback = false;

            await Task.Run(async () =>
            {
                int retries = 10;
                while (RouteVideo.Duration.TotalSeconds == 0 && retries > 0)
                {
                    await Task.Delay(100);
                    retries--;
                }

                if (RouteVideo.Duration.TotalSeconds > 0)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        FastForwardVideo();
                    });
                }
            });
        }
        else
        {
            RouteVideo.ShouldLoopPlayback = true;
            RouteVideo.Speed = 1;
        }

        RewindButton.IsVisible = _hasVideo;
        ForwardButton.IsVisible = _hasVideo;

        RouteVideo.ShouldAutoPlay = false;
        RouteVideo.Pause();
        RouteVideo.IsVisible = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Unsubscribe from the MediaOpened event
        RouteVideo.MediaOpened -= OnMediaOpened;
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

            bool isAtFinish = _nextMilestoneIndex > 0 && _milestones[_nextMilestoneIndex - 1] == 0.993m;
            if (!_isFirstSegment && !isAtFinish)
                StopPulseAnimation();

            PlotView.Model = _graphPlotter.RenderDistanceOverlay(totalDistanceInMeters, nextSegmentInMeters);
        });
    }

    private void OnStatisticsUpdate(PlayerStatistics stats)
    {
        // Ensure UI updates are on the main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Ensure that the totaldistance does not exceed the totaldistanceInMeters
            if (stats.CurrentDistanceM > _gpxProcessor.TotalDistanceInMeters)
            {
                stats.CurrentDistanceM = _gpxProcessor.TotalDistanceInMeters;
            }

            DistanceLabel.Text = $"{stats.CurrentDistanceM:N0} / {_gpxProcessor.TotalDistanceInMeters:N0}";
            SegmentLabel.Text = $"{stats.SegmentRemainingM:N0}";
            SpeedLabel.Text = $"{stats.CurrentSpeedMinKM:mm\\:ss}";
            HeartrateLabel.Text = stats.CurrentHeartRate > 0 ? $"{stats.CurrentHeartRate}" : "--";
            AscendLabel.Text = $"{stats.TotalInclinationM:N0}";

            if (stats.CurrentHeartRate.HasValue)
                stats.HeartRateSamples.Add(stats.CurrentHeartRate.Value);

            // store the statistics for later use
            _playerStatistics = stats;

            // change video speed if needed.... otherwise use the actual speed
            if (_actualSpeed != stats.CurrentSpeedKMH)
            {
                _actualSpeed = stats.CurrentSpeedKMH;
                UpdateRouteVideoSpeed();
            }

            // Check milestone
            decimal totalDistance = _gpxProcessor.TotalDistanceInMeters;
            decimal currentDistance = stats.CurrentDistanceM ?? 0;

            if (_nextMilestoneIndex < _milestones.Length)
            {
                decimal milestoneDistance = totalDistance * _milestones[_nextMilestoneIndex];
                if (currentDistance >= milestoneDistance)
                {
                    int? repeat = (_milestones[_nextMilestoneIndex] == 0.993m) ? null : 3;
                    StartPulseAnimation(repeat, _milestones[_nextMilestoneIndex]);
                    _nextMilestoneIndex++;
                }
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
            {
                double newSpeed = (secondsToGo > 0) ? (remainingVideoSeconds / secondsToGo) : 0;
                RouteVideo.Speed = newSpeed;
            }
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

    private void FastForwardVideo()
    {
        // we have a list of seconds to skip, so we can fast forward the video
        // we adjust the playback speed per item in the list
        // because we can calculate the remaining dis
        decimal? totalDistanceM = _gpxProcessor.TotalOriginalLengthInMeters;
        decimal? distanceCoveredM = 0;
        double videoPositionSeconds = 0;
        for (int i = 0; i < _gpxProcessor.SecondsDistancesSpeedsToSkip.Length; i++)
        {
            distanceCoveredM += _gpxProcessor.SecondsDistancesSpeedsToSkip[i].distance;

            // calculate the position and speed of the runner in this segment
            // we need this to know the remaining time to run
            decimal speedKmh = _gpxProcessor.SecondsDistancesSpeedsToSkip[i].speed;
            double remainingDistanceM = (double)(totalDistanceM - distanceCoveredM);
            double secondsToGo = Math.Max(0, remainingDistanceM / (double)(speedKmh / 3.6m));

            // for the video we need to know how many seconds are left to play
            // based on that we know what playback speed to use. 
            double remainingVideoSeconds = RouteVideo.Duration.TotalSeconds - videoPositionSeconds;
            double speed = (secondsToGo > 0) ? (remainingVideoSeconds / secondsToGo) : 0;

            // This playback factor can be used to calculate the time that the track is recorded in the video
            videoPositionSeconds += (double)_gpxProcessor.SecondsDistancesSpeedsToSkip[i].seconds * speed;
        }

        if (videoPositionSeconds > 0)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                RouteVideo.SeekTo(TimeSpan.FromSeconds(videoPositionSeconds));
            });
        }
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        // Verberg de rewind- en forward-knoppen
        RewindButton.IsVisible = false;
        ForwardButton.IsVisible = false;

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
        // ensure we have the proper gpx data that we can send out (dates, heart rate, distance, etc.)
        _gpxProcessor.UpdateGpxData(_playerStatistics.CurrentDistanceM);
        string updatedGpxData = _gpxProcessor.GetSerializedGpxData();

        // always write out the last run gpx (can be used for troubleshooting)
        var fileName = Path.Combine(Runtime.GetAppFolder(), "LastRun.gpx");
        File.WriteAllText(fileName, updatedGpxData);

        await Navigation.PushAsync(new SummaryScreen(_playerStatistics, updatedGpxData));
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
        StartPulseImage.WidthRequest = 600;
        StartPulseImage.HeightRequest = 250;
    }

    private async void StartPulseAnimation(int? repeatCount = null, decimal milestone = 0)
    {
        if (_pulseActive) return;

        _pulseActive = true;
        StartPulseImage.IsVisible = true;

        // Kies juiste afbeelding
        switch (milestone)
        {
            case 0.25m:
                StartPulseImage.Source = "checkpoint.png";
                break;
            case 0.5m:
                StartPulseImage.Source = "halfway.png";
                break;
            case 0.75m:
                StartPulseImage.Source = "almostthere.png";
                break;
            case 0.993m:
                StartPulseImage.Source = "finish.png";
                break;
        }

        int repeats = 0;
        bool infinite = (milestone >= 0.993m) && repeatCount == null;
        while (_pulseActive && (infinite || repeatCount == null || repeats < repeatCount))
        {
            await StartPulseImage.ScaleTo(1.10, 800, Easing.SinInOut);
            await StartPulseImage.ScaleTo(0.90, 800, Easing.SinInOut);
            repeats++;
        }

        // only stop when not the finish animation
        if (!infinite) StopPulseAnimation();
    }

    private void OnRewindClicked(object sender, EventArgs e)
    {
        if (_hasVideo && RouteVideo.Position.TotalSeconds > 2)
        {
            RouteVideo.SeekTo(RouteVideo.Position - TimeSpan.FromSeconds(2));
        }
    }

    private void OnForwardClicked(object sender, EventArgs e)
    {
        if (_hasVideo && RouteVideo.Position.TotalSeconds + 2 < RouteVideo.Duration.TotalSeconds)
        {
            RouteVideo.SeekTo(RouteVideo.Position + TimeSpan.FromSeconds(2));
        }
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        if (RouteVideo.Duration.TotalSeconds > 0)
        {
            // Perform actions that depend on the video duration
            if (_gpxProcessor?.SecondsDistancesSpeedsToSkip.Length > 0)
            {
                FastForwardVideo();
            }
        }
    }
}
