namespace Re_RunApp.Views;

using OxyPlot;
using Re_RunApp.Core;
using System.Runtime.CompilerServices;

public partial class RouteDetailsScreen : ContentPage
{
    private GpxProcessor _gpxProcessor = new GpxProcessor();
    private readonly string _gpxFilePath;
    private bool _useSimulation = false;
    private bool _startButtonEnabled = false;
    private bool _heartRateEnabled = false;
    private decimal _skipMeters = 0;

    public RouteDetailsScreen(string gpxFilePath)
    {
        InitializeComponent();

        _gpxFilePath = gpxFilePath;

        this.Loaded += RouteDetailsScreen_Loaded;
        this.Unloaded += RouteDetailsScreen_Unloaded;
    }


    private void RouteDetailsScreen_Loaded(object? sender, EventArgs e)
    { 
    }

    
    private void RouteDetailsScreen_Unloaded(object? sender, EventArgs e)
    {
        PlotView.Model = null;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        LoadRouteDetails();
        LoadSpeedSettings(); // Load speed settings if the file exists
        _skipMeters = 0;
        _useSimulation = false;
        _startButtonEnabled = false;
        _heartRateEnabled = false;

        var graphPlotter = new GraphPlotter();
        PlotView.Model = graphPlotter.PlotGraph(_gpxProcessor, false);

        _heartRateEnabled = await Runtime.HeartRate.ConnectToDevice(false);
        _startButtonEnabled =  await Runtime.Treadmill.ConnectToDevice(false);
    
        StartButton.IsEnabled = _startButtonEnabled;
        if (_heartRateEnabled) StartButton.Text = "Start + ❤";
    }

    private async void OnConnectTreadmillClicked(object sender, EventArgs e)
    {
        Runtime.Treadmill.DeleteDeviceIdFile();

        _startButtonEnabled = await Runtime.Treadmill.ConnectToDevice(true);
        StartButton.IsEnabled = _startButtonEnabled;
    }

    private async void OnConnectHeartRateClicked(object sender, EventArgs e)
    {
        Runtime.HeartRate.Enabled = true;
        Runtime.HeartRate.DeleteDeviceIdFile();

        try
        {
            _heartRateEnabled = await Runtime.HeartRate.ConnectToDevice(true);
            StartButton.Text = _heartRateEnabled ? "Start + ❤" : "Start";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to heart rate device: {ex.Message}");
            await DisplayAlert("Error", "An error occurred while connecting to the heart rate device.", "OK");
        }
    }

    private void LoadRouteDetails()
    {
        _gpxProcessor.LoadGpxData(_gpxFilePath);
        _gpxProcessor.GetRun();
        
        // Set route details
        RouteNameLabel.Text = _gpxProcessor.Gpx?.trk.name;
        TotalDistanceLabel.Text = $"{_gpxProcessor.TotalDistanceInMeters / 1000:F1}";
        TotalElevationLabel.Text = $"{(_gpxProcessor.FindMaximumElevation() - _gpxProcessor.FindMinimumElevation()):F0}";
        TotalAscendLabel.Text = $"{_gpxProcessor.TotalElevationInMeters:F0}";
        RouteStartSlider.Value = 1;
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        SaveSpeedSettings();

        if (!_useSimulation)
        {
            Runtime.HeartRate.Enabled = _heartRateEnabled;
        }
        await Navigation.PushAsync(new ActivityScreen(_gpxFilePath, _useSimulation, _skipMeters));
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

        var settings = new RunSettings
        {
            Speed0to5 = double.Parse(Speed0to5Label.Text, System.Globalization.CultureInfo.InvariantCulture),
            Speed6to8 = double.Parse(Speed6to8Label.Text, System.Globalization.CultureInfo.InvariantCulture),
            Speed8to10 = double.Parse(Speed8to10Label.Text, System.Globalization.CultureInfo.InvariantCulture),
            Speed11to12 = double.Parse(Speed11to12Label.Text, System.Globalization.CultureInfo.InvariantCulture),
            Speed13to15 = double.Parse(Speed13to15Label.Text, System.Globalization.CultureInfo.InvariantCulture),
            AutoSpeedControl = AutoSpeedControlCheckBox.IsChecked,
            Name = _gpxProcessor.Gpx.trk.name,
            TotalDistance = _gpxProcessor.TotalDistanceInMeters,
            TotalAscend = _gpxProcessor.TotalElevationInMeters,
            // todo
        };
        File.WriteAllText(speedSettingsFilePath, System.Text.Json.JsonSerializer.Serialize(settings));
        
        // update the speedsettings while saving...
        Runtime.RunSettings = settings;

    }

    private void LoadSpeedSettings()
    {
        string speedSettingsFilePath = Path.ChangeExtension(_gpxFilePath, ".speedsettings");

        if (File.Exists(speedSettingsFilePath))
        {
            string jsonContent = File.ReadAllText(speedSettingsFilePath);
            var settings = System.Text.Json.JsonSerializer.Deserialize<RunSettings>(jsonContent);

            if (settings != null)
            {
                Speed0to5Label.Text = settings.Speed0to5.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                Speed6to8Label.Text = settings.Speed6to8.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                Speed8to10Label.Text = settings.Speed8to10.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                Speed11to12Label.Text = settings.Speed11to12.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                Speed13to15Label.Text = settings.Speed13to15.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
                AutoSpeedControlCheckBox.IsChecked = settings.AutoSpeedControl;

                RouteStartSlider.IsEnabled = AutoSpeedControlCheckBox.IsChecked;
            }
            Runtime.RunSettings = settings;
        }
    }
    
    private void OnHyperlinkTapped(object sender, EventArgs e)
    {
        // toggle the simulation
        _useSimulation = !_useSimulation;
        StartButton.IsEnabled = _useSimulation || _startButtonEnabled;

        SimulatorLabel.Text = (StartButton.IsEnabled) ? "Treadmill" : "Simulator";

        string msg = _useSimulation ? "The treadmill and heartrate sensor will be simulated!" : "Use actual treadmill and heartrate sensor will be used!";
        DisplayAlert("Developer Information", msg, "OK");
    }

    private void OnRouteStartSliderValueChanged(object sender, ValueChangedEventArgs e)
    {
        // determine the start point of the route (how many meters to skip) 
        _skipMeters = (1 - (decimal)e.NewValue) * _gpxProcessor.TotalDistanceInMeters;

    }

    private void OnAutoSpeedControlCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        // Check the new state of the checkbox
        RouteStartSlider.IsEnabled = e.Value;

        if (!RouteStartSlider.IsEnabled )
        {
            RouteStartSlider.Value=1;
            _skipMeters = 0; // Reset skip meters if auto speed control is disabled
        }
    }
}
