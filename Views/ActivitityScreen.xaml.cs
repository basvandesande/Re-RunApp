namespace Re_RunApp.Views;

using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
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
            ElevationGraphImage.Source = ImageSource.FromStream(() => _graphPlotter.RenderDistanceOverlay(totalDistanceInMeters)); 
        });
    }

    private void OnStatisticsUpdate(PlayerStatistics stats)
    {
        // Ensure UI updates are on the main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DistanceLabel.Text = $"{stats.TotalDistanceM:N0}";
            TimeLabel.Text = $"{TimeSpan.FromSeconds(stats.SecondsElapsed):hh\\:mm\\:ss}";
            SpeedLabel.Text = $"{stats.CurrentSpeedMinKM:mm\\:ss}";
            InclinationLabel.Text = $"{stats.SegmentIncrementPercentage:N1}";
            HeartrateLabel.Text = $"{stats.CurrentHeartRate}";
            TotalClimbedLabel.Text = $"{stats.TotalInclinationM:N0}";
            TotalDescendedLabel.Text = $"{stats.TotalDeclinationM:N0}";
            SegmentRemainingLabel.Text = $"{stats.SegmentRemainingM:N0}";


            // store the statistics for later use
            _playerStatistics = stats;
        });
    }


    private void ForceVideoScaling()
    {
        if (RouteVideo.IsVisible)
        {
            double containerWidth = VideoFrame.Width;
            double containerHeight = VideoFrame.Height;

            double aspectRatio = containerHeight / containerWidth ;   //  9.0/16.0
            double videoWidth = containerWidth;
            double videoHeight = videoWidth * aspectRatio;

            if (videoHeight > containerHeight)
            {
                videoHeight = containerHeight;
                videoWidth = videoHeight / aspectRatio;
            }

            RouteVideo.WidthRequest = videoWidth;
            RouteVideo.HeightRequest = videoHeight;
        }
    }


    private async void OnStartClicked(object sender, EventArgs e)
    {   
        // swap buttons
        StartButton.IsVisible = false;
        StopButton.IsVisible = true;
        FinishButton.IsVisible = false;
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
        await _player.StopAsync();
    }



    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (ElevationGraphImage.Width > 0 && ElevationGraphImage.Height > 0)
        {
            var elevationBitmap = _graphPlotter.PlotGraph(_gpxProcessor, (int)ElevationGraphImage.Height, (int)ElevationGraphImage.Width);
            ElevationGraphImage.Source = ImageSource.FromStream(() => elevationBitmap);
            ElevationGraphImage.Source = ImageSource.FromStream(() => _graphPlotter.RenderDistanceOverlay((decimal)_playerStatistics.TotalDistanceM));
        }

        string videoPath = Path.ChangeExtension(_gpxFilePath, ".mp4");
        if (File.Exists(videoPath))
        {
            RouteVideo.Source = MediaSource.FromFile(videoPath);
        }
        else
        {
            RouteVideo.ShouldLoopPlayback = true;
        }
        RouteVideo.IsVisible = true;
        ForceVideoScaling();

    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        _treadmill.Disconnect();
        _heartRate.Disconnect();
    }
}
