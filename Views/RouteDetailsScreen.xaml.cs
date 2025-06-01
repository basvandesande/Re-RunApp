namespace Re_RunApp.Views;

using Re_RunApp.Core;

public partial class RouteDetailsScreen : ContentPage
{
    private readonly string _gpxFilePath;
    private bool _graphGenerated = false;
    private DateTime _lastTapTime = DateTime.MinValue;
    private const int _doubleTapThresholdMs = 400; // Adjust as needed
    private bool _useSimulation = false;
    private bool _startButtonEnabled = false;

    public RouteDetailsScreen(string gpxFilePath)
    {
        InitializeComponent();
        _gpxFilePath = gpxFilePath;

        LoadRouteDetails();
        LoadSpeedSettings(); // Load speed settings if the file exists
    }


    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        var connected =  await Runtime.Treadmill.ConnectToDevice(false); 
        StartButton.IsEnabled = _startButtonEnabled;
    }

    private async void OnConnectTreadmillClicked(object sender, EventArgs e)
    {
        Runtime.Treadmill.DeleteDeviceIdFile();

        _startButtonEnabled = await Runtime.Treadmill.ConnectToDevice(true);
        StartButton.IsEnabled = _startButtonEnabled;
    }

    private async void OnConnectHeartRateClicked(object sender, EventArgs e)
    {
        Runtime.HeartRate.DeleteDeviceIdFile();

        var connected = await Runtime.HeartRate.ConnectToDevice(true);
        Runtime.HeartRate.Enabled=connected;
    }

    private void LoadRouteDetails()
    {
        var gpxProcessor = new GpxProcessor();
        gpxProcessor.LoadGpxData(_gpxFilePath);
        gpxProcessor.GetRun();

        // Set route details
        RouteNameLabel.Text = $"Name: {gpxProcessor.Gpx?.trk.name}";
        TotalDistanceLabel.Text = $"Total Distance: {gpxProcessor.TotalDistanceInMeters / 1000:F1} km";
        TotalElevationLabel.Text = $"Total Elevation: {(gpxProcessor.FindMaximumElevation() - gpxProcessor.FindMinimumElevation()):F0} m";
        TotalAscendLabel.Text = $"Total Ascend: {gpxProcessor.TotalElevationInMeters:F0}  m";
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        SaveSpeedSettings();

        if (!Runtime.HeartRate.Enabled)
        {
            var connected = await Runtime.HeartRate.ConnectToDevice(true); 
            Runtime.HeartRate.Enabled = connected;
        }
        await Navigation.PushAsync(new ActivityScreen(_gpxFilePath, _useSimulation));
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
        string speedSettingsFilePath = Path.ChangeExtension(_gpxFilePath, ".speedsettings");

        var settings = new SpeedSettings
        {
            Speed0to5 = double.Parse(Speed0to5Label.Text, System.Globalization.CultureInfo.InvariantCulture),
            Speed6to8 = double.Parse(Speed6to8Label.Text, System.Globalization.CultureInfo.InvariantCulture),
            Speed8to10 = double.Parse(Speed8to10Label.Text, System.Globalization.CultureInfo.InvariantCulture),
            Speed11to12 = double.Parse(Speed11to12Label.Text, System.Globalization.CultureInfo.InvariantCulture),
            Speed13to15 = double.Parse(Speed13to15Label.Text, System.Globalization.CultureInfo.InvariantCulture),
            AutoSpeedControl = AutoSpeedControlCheckBox.IsChecked
        };
        File.WriteAllText(speedSettingsFilePath, System.Text.Json.JsonSerializer.Serialize(settings));
        
        // update the speedsettings while saving...
        Runtime.SpeedSettings = settings;

    }

    private void LoadSpeedSettings()
    {
        string speedSettingsFilePath = Path.ChangeExtension(_gpxFilePath, ".speedsettings");

        if (File.Exists(speedSettingsFilePath))
        {
            string jsonContent = File.ReadAllText(speedSettingsFilePath);
            var settings = System.Text.Json.JsonSerializer.Deserialize<SpeedSettings>(jsonContent);

            if (settings != null)
            {
                Speed0to5Label.Text = settings.Speed0to5.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                Speed6to8Label.Text = settings.Speed6to8.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                Speed8to10Label.Text = settings.Speed8to10.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                Speed11to12Label.Text = settings.Speed11to12.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                Speed13to15Label.Text = settings.Speed13to15.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                AutoSpeedControlCheckBox.IsChecked = settings.AutoSpeedControl;
            }

            // initialize the speedsettings
            Runtime.SpeedSettings = settings;
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

    private void OnElevationGraphTapped(object sender, TappedEventArgs e)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastTapTime).TotalMilliseconds < _doubleTapThresholdMs)
        {
            // Double-tap detected
            OnElevationGraphDoubleTapped(sender, e);
            _lastTapTime = DateTime.MinValue; // Reset
        }
        else
        {
            _lastTapTime = now;
        }
    }

    private void OnElevationGraphDoubleTapped(object sender, TappedEventArgs e)
    {
        // toggle the simulation
        _useSimulation = !_useSimulation;
        StartButton.IsEnabled = _useSimulation || _startButtonEnabled;

        string msg = _useSimulation ? "Use treadmill simulation!" : "Use actual treadmill!";
        DisplayAlert("Developer Information", msg, "OK");
    }

    
}
