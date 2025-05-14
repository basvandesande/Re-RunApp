namespace Re_RunApp.Views;

using Re_RunApp.Core;

public partial class RouteDetailsScreen : ContentPage
{
    private readonly string _gpxFilePath;

    private bool _graphGenerated = false;

    public RouteDetailsScreen(string gpxFilePath)
    {
        InitializeComponent();
        _gpxFilePath = gpxFilePath;

        LoadRouteDetails();
        LoadSpeedSettings(); // Load speed settings if the file exists
    }

    private void LoadRouteDetails()
    {
        var gpxProcessor = new GpxProcessor();
        gpxProcessor.LoadGpxData(_gpxFilePath);
        gpxProcessor.GetRun();

        // Set route details
        RouteNameLabel.Text = $"Name: {gpxProcessor.Gpx.trk.name}";
        TotalDistanceLabel.Text = $"Total Distance: {gpxProcessor.TotalDistanceInMeters / 1000:F1} km";
        TotalElevationLabel.Text = $"Total Elevation: {(gpxProcessor.FindMaximumElevation() - gpxProcessor.FindMinimumElevation()):F0} m";
        TotalAscendLabel.Text = $"Total Ascend: {gpxProcessor.TotalElevationInMeters:F0}  m";
        
      
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        // Save the speed settings to a file
        SaveSpeedSettings();

        // Collect speed settings for display or further processing
        var speedSettings = new Dictionary<string, double>
        {
            { "0-5%", double.Parse(Speed0to5Label.Text, System.Globalization.CultureInfo.InvariantCulture) },
            { "6-8%", double.Parse(Speed6to8Label.Text, System.Globalization.CultureInfo.InvariantCulture) },
            { "8-10%", double.Parse(Speed8to10Label.Text, System.Globalization.CultureInfo.InvariantCulture) },
            { "11-12%", double.Parse(Speed11to12Label.Text, System.Globalization.CultureInfo.InvariantCulture) },
            { "13-15%", double.Parse(Speed13to15Label.Text, System.Globalization.CultureInfo.InvariantCulture) }
        };

        // Display the speed settings or pass them to the next screen
        await DisplayAlert("Speed Settings", string.Join("\n", speedSettings.Select(kv => $"{kv.Key}: {kv.Value} km/h")), "OK");
        await Navigation.PushAsync(new ActivityScreen());

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
    private void SaveSpeedSettings()
    {
        // Construct the file path with the same name as the GPX file but with a .speedsettings extension
        string speedSettingsFilePath = Path.ChangeExtension(_gpxFilePath, ".speedsettings");

        // Create a dictionary of speed settings
        var speedSettings = new Dictionary<string, double>
        {
            { "0-5%", double.Parse(Speed0to5Label.Text, System.Globalization.CultureInfo.InvariantCulture) },
            { "6-8%", double.Parse(Speed6to8Label.Text, System.Globalization.CultureInfo.InvariantCulture) },
            { "8-10%", double.Parse(Speed8to10Label.Text, System.Globalization.CultureInfo.InvariantCulture) },
            { "11-12%", double.Parse(Speed11to12Label.Text, System.Globalization.CultureInfo.InvariantCulture) },
            { "13-15%", double.Parse(Speed13to15Label.Text, System.Globalization.CultureInfo.InvariantCulture) }
        };

        // Serialize the dictionary to JSON and save it to the file
        File.WriteAllText(speedSettingsFilePath, System.Text.Json.JsonSerializer.Serialize(speedSettings));
    }

    private void LoadSpeedSettings()
    {
        // Construct the file path with the same name as the GPX file but with a .speedsettings extension
        string speedSettingsFilePath = Path.ChangeExtension(_gpxFilePath, ".speedsettings");

        // Check if the file exists
        if (File.Exists(speedSettingsFilePath))
        {
            // Read the JSON content from the file
            string jsonContent = File.ReadAllText(speedSettingsFilePath);

            // Deserialize the JSON content into a dictionary
            var speedSettings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(jsonContent);

            // Set the speed values in the controls
            if (speedSettings != null)
            {
                Speed0to5Label.Text = speedSettings["0-5%"].ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                Speed6to8Label.Text = speedSettings["6-8%"].ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                Speed8to10Label.Text = speedSettings["8-10%"].ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                Speed11to12Label.Text = speedSettings["11-12%"].ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                Speed13to15Label.Text = speedSettings["13-15%"].ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (ElevationGraphImage.Width > 0 && ElevationGraphImage.Height > 0)
        {
            // Generate elevation graph using the actual width and height
            var gpxProcessor = new GpxProcessor();
            gpxProcessor.LoadGpxData(_gpxFilePath);
            gpxProcessor.GetRun();

            var graphPlotter = new GraphPlotter();
            var elevationBitmap = graphPlotter.PlotGraph(gpxProcessor, (int)ElevationGraphImage.Height, (int)ElevationGraphImage.Width);

            // Set the ImageSource
            ElevationGraphImage.Source = ImageSource.FromStream(() => elevationBitmap);

            // Optionally, prevent redundant calls by setting a flag
            if (_graphGenerated) return;
            _graphGenerated = true;
        }
    }
}
