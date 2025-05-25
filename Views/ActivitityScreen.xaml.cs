namespace Re_RunApp.Views;

using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;
using Re_RunApp.Core;

public partial class ActivityScreen : ContentPage
{
    private string _gpxFilePath;
    private GpxProcessor _gpxProcessor = new GpxProcessor();
    private Treadmill _treadmill = new Treadmill();
    private HeartRate _heartRate = new HeartRate();
    private GraphPlotter _graphPlotter = new GraphPlotter();
    private Player _player;

    public ActivityScreen(string gpxFilePath)
    {
        InitializeComponent();

        _gpxFilePath = gpxFilePath;
        _gpxProcessor.LoadGpxData(_gpxFilePath);
        _gpxProcessor.GetRun();

        _treadmill = Runtime.Treadmill;
        _heartRate = Runtime.HeartRate;

        _player = new Player(_gpxProcessor, _treadmill, _heartRate);
        _player.OnStatisticsUpdate += OnStatisticsUpdate; 
       
    }

    private void OnStatisticsUpdate(PlayerStatistics stats)
    {
        // Ensure UI updates are on the main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DistanceLabel.Text = $"Distance: {stats.TotalDistanceM:N0} meter";
            TimeLabel.Text = $"Elapsed time: {TimeSpan.FromSeconds(stats.SecondsElapsed):hh\\:mm\\:ss}";
            SpeedLabel.Text = $"Speed: {stats.CurrentSpeedKMH:F1} km/h - ({stats.CurrentSpeedMinKM:mm\\:ss})";
            //// Voeg hier eventueel meer statistieken toe
        });
    }


    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (ElevationGraphImage.Width > 0 && ElevationGraphImage.Height > 0)
        {
            var elevationBitmap = _graphPlotter.PlotGraph(_gpxProcessor, (int)ElevationGraphImage.Height, (int)ElevationGraphImage.Width);
            ElevationGraphImage.Source = ImageSource.FromStream(() => elevationBitmap);
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



    private async void OnFinishClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SummaryScreen());
    }
}
