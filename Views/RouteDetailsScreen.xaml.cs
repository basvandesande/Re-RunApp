namespace Re_RunApp.Views;

using Re_RunApp.Core;

public partial class RouteDetailsScreen : ContentPage
{
    private readonly string _gpxFilePath;

    public RouteDetailsScreen(string gpxFilePath)
    {
        InitializeComponent();
        _gpxFilePath = gpxFilePath;

        LoadRouteDetails();
    }

    private void LoadRouteDetails()
    {
        var gpxProcessor = new GpxProcessor();
        gpxProcessor.LoadGpxData(_gpxFilePath);
        gpxProcessor.GetRun();

        // Set route details
        RouteNameLabel.Text = $"Name: {gpxProcessor.Gpx.trk.name}";
        TotalDistanceLabel.Text = $"Total Distance: {gpxProcessor.TotalDistanceInMeters / 1000:F1} km";
        TotalElevationLabel.Text = $"Total Elevation: {gpxProcessor.TotalElevationInMeters:F0} m";

        // Generate elevation graph using GraphPlotter
        var graphPlotter = new GraphPlotter();
        var elevationBitmap = graphPlotter.PlotGraph(gpxProcessor);

        // Convert the bitmap to an ImageSource
        ElevationGraphImage.Source = ImageSource.FromStream(() => elevationBitmap);
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        // Collect speed settings
        var speedSettings = new Dictionary<string, double>
        {
            { "0-5%", 8.0 },
            { "6-8%", 7.8 },
            { "8-10%", 7.0 },
            { "11-12%", 6.8 },
            { "13-15%", 6.3 }
        };

        // Pass speed settings to the next screen or process them
        await DisplayAlert("Speed Settings", string.Join("\n", speedSettings.Select(kv => $"{kv.Key}: {kv.Value} km/h")), "OK");
    }
    private void OnIncreaseSpeed0to5(object sender, EventArgs e)
    {
        UpdateSpeed(Speed0to5Label, 0.1);
    }

    private void OnDecreaseSpeed0to5(object sender, EventArgs e)
    {
        UpdateSpeed(Speed0to5Label, -0.1);
    }

    private void OnIncreaseSpeed6to8(object sender, EventArgs e)
    {
        UpdateSpeed(Speed6to8Label, 0.1);
    }

    private void OnDecreaseSpeed6to8(object sender, EventArgs e)
    {
        UpdateSpeed(Speed6to8Label, -0.1);
    }

    private void OnIncreaseSpeed8to10(object sender, EventArgs e)
    {
        UpdateSpeed(Speed8to10Label, 0.1);
    }

    private void OnDecreaseSpeed8to10(object sender, EventArgs e)
    {
        UpdateSpeed(Speed8to10Label, -0.1);
    }

    private void OnIncreaseSpeed11to12(object sender, EventArgs e)
    {
        UpdateSpeed(Speed11to12Label, 0.1);
    }

    private void OnDecreaseSpeed11to12(object sender, EventArgs e)
    {
        UpdateSpeed(Speed11to12Label, -0.1);
    }

    private void OnIncreaseSpeed13to15(object sender, EventArgs e)
    {
        UpdateSpeed(Speed13to15Label, 0.1);
    }

    private void OnDecreaseSpeed13to15(object sender, EventArgs e)
    {
        UpdateSpeed(Speed13to15Label, -0.1);
    }

    private void UpdateSpeed(Label speedLabel, double delta)
    {
        if (double.TryParse(speedLabel.Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double currentSpeed))
        {
            currentSpeed = Math.Max(0, currentSpeed + delta); // Ensure speed is not negative
            speedLabel.Text = currentSpeed.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
