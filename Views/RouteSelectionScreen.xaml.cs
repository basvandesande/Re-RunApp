namespace Re_RunApp.Views;

using CommunityToolkit.Maui.Views;
using Re_RunApp.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

public partial class RouteSelectionScreen : ContentPage
{
    private CancellationTokenSource? _resizeCts;

    public RouteSelectionScreen()
    {
        InitializeComponent();
        this.Loaded += RouteSelectionScreen_Loaded;

        // handle page size changes (fired during window resize / orientation change)
        this.SizeChanged += RouteSelectionScreen_SizeChanged;
    }

    private void RouteSelectionScreen_SizeChanged(object? sender, EventArgs e)
    {
        // debounce frequent SizeChanged events
        _resizeCts?.Cancel();
        _resizeCts = new CancellationTokenSource();
        var ct = _resizeCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                // small delay to wait for resize to settle
                await Task.Delay(120, ct);
                if (ct.IsCancellationRequested) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // recompute sizes and force layout/measure
                    ForceVideoScaling();

                    // adjust date label font to fit the visible video width
                    AdjustDateFontSize();

                    // request re-measure/re-layout for the video and page
                    RouteVideo?.InvalidateMeasure();
                    this?.InvalidateMeasure();
                });
            }
            catch (TaskCanceledException)
            {
                // expected on debounce
            }
            catch
            {
                // ignore other issues here
            }
        }, ct);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _resizeCts?.Cancel();
        this.SizeChanged -= RouteSelectionScreen_SizeChanged;
    }

    private void RouteSelectionScreen_Loaded(object? sender, EventArgs e)
    {
        string folder = Runtime.GetAppFolder();
        string[] files = Directory.GetFiles(folder, "*.gpx");

        // remove LastRun.gpx from the list of files
        files = files.Where(f => !f.EndsWith("LastRun.gpx", StringComparison.OrdinalIgnoreCase)).ToArray();

        var fileList = files.Select(file => new
        {
            FullPath = file,
            FileName = Path.GetFileName(file)
        }).ToList();

        RouteListView.ItemsSource = fileList;

        if (fileList.Count > 0)
        {
            RouteListView.SelectedItem = fileList[0];
        }

        // Show/hide the Open Folder button based on platform (if it exists)
        try
        {
            if (OpenFolderButton != null)
            {
                OpenFolderButton.IsVisible = DeviceInfo.Platform == DevicePlatform.WinUI || 
                                           DeviceInfo.Platform == DevicePlatform.MacCatalyst ||
                                           OperatingSystem.IsWindows(); // Additional check for Windows
            }
        }
        catch
        {
            // Button might not be loaded yet, ignore
        }
    }

    private async void OnOpenFolderClicked(object sender, EventArgs e)
    {
        try
        {
            string folderPath = Runtime.GetAppFolder();

            await DisplayAlert("Data folder location", $"The .gpx and .mp4 files can be stored in this folder:\r\n\r\n{folderPath}\r\n\r\nThe .gpx and .mp4 file should have the same name!", "OK");


            // Debug information
            System.Diagnostics.Debug.WriteLine($"Attempting to open folder: {folderPath}");
            System.Diagnostics.Debug.WriteLine($"Current Platform: {DeviceInfo.Platform}");
            
            // Check if directory exists before the call
            bool existsBeforeCall = Directory.Exists(folderPath);
            System.Diagnostics.Debug.WriteLine($"Directory exists before call: {existsBeforeCall}");
            
            bool success = Runtime.OpenFolderInExplorer(folderPath);
            
            // Check if directory exists after the call
            bool existsAfterCall = Directory.Exists(folderPath);
            System.Diagnostics.Debug.WriteLine($"Directory exists after call: {existsAfterCall}");
            
            if (!success)
            {
                // Show detailed information in the alert
                string platformInfo = $"Platform: {DeviceInfo.Platform}\n";
                string pathInfo = $"Folder path: {folderPath}\n";
                string existsBeforeInfo = $"Existed before call: {existsBeforeCall}\n";
                string existsAfterInfo = $"Exists after call: {existsAfterCall}\n";
                string parentDirInfo = $"Parent directory exists: {Directory.Exists(Path.GetDirectoryName(folderPath))}\n";
                
                await DisplayAlert("Debug Information", 
                    $"Could not open folder automatically.\n\n{platformInfo}{pathInfo}{existsBeforeInfo}{existsAfterInfo}{parentDirInfo}", 
                    "OK");
            }
            else
            {
                // Remove the success dialog - it's annoying for users
                // Just log success instead
                System.Diagnostics.Debug.WriteLine($"Successfully opened folder: {folderPath}");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", 
                $"Failed to open folder: {ex.Message}\n\nPlatform: {DeviceInfo.Platform}\n\nStack trace: {ex.StackTrace}", 
                "OK");
        }
    }

    private void OnRouteSelected(object sender, SelectedItemChangedEventArgs e)
    {
        // enable/disable next button immediately
        NextButton.IsEnabled = (e.SelectedItem is not null);

        if (e.SelectedItem is null)
            return;

        // If page is not yet loaded, defer processing once (no recursion)
        if (!this.IsLoaded)
        {
            var selected = e.SelectedItem;
            EventHandler? handler = null;
            handler = (s, ev) =>
            {
                this.Loaded -= handler;
                MainThread.BeginInvokeOnMainThread(() => ProcessSelection(selected));
            };
            this.Loaded += handler;
            return;
        }

        // Page is loaded - process selection now
        ProcessSelection(e.SelectedItem);
    }

    // Move selection processing to a separate method to avoid recursion and make error handling clearer
    private void ProcessSelection(object selectedItem)
    {
        if (selectedItem is null) return;

        try
        {
            var selectedRoute = (dynamic)selectedItem;
            string fullPath = selectedRoute.FullPath;

            // Load route details (may throw on corrupt GPX)
            var routeDetails = GetRouteDetails(fullPath);

            TitleLabel.Text = routeDetails.Title;
            DistanceLabel.Text = $"{routeDetails.Distance / 1000:F1}";
            ElevationLabel.Text = $"{routeDetails.Elevation:F0}";
            DateLabel.Text = $"{routeDetails.Date:ddd, d MMMM yyyy}";

            // Try to load associated video; fall back gracefully
            string videoPath = Path.ChangeExtension(fullPath, ".mp4");
            if (File.Exists(videoPath))
            {
                RouteVideo.Source = MediaSource.FromFile(videoPath);
            }
            else
            {
                try
                {
                    RouteVideo.Source = MediaSource.FromResource("nomedia.mp4");
                }
                catch
                {
                    RouteVideo.Source = null;
                }
            }

            ForceVideoScaling();
        }
        catch (Exception ex)
        {
            // don't crash the UI on bad GPX or IO issues - log and show minimal info
            System.Diagnostics.Debug.WriteLine($"Error processing selected route: {ex.Message}");
            try
            {
                // best-effort: show filename if possible
                var selectedRoute = (dynamic)selectedItem;
                TitleLabel.Text = Path.GetFileNameWithoutExtension((string)selectedRoute.FullPath);
            }
            catch { }

            DistanceLabel.Text = "--";
            ElevationLabel.Text = "--";
            DateLabel.Text = string.Empty;
            RouteVideo.Source = null;
        }
    }

    private (string Title, decimal Distance, decimal Elevation, DateTime Date) GetRouteDetails(string fullPath)
    {
        var gpxProcessor = new GpxProcessor();
        gpxProcessor.LoadGpxData(fullPath);
        gpxProcessor.GetRun();

        return (gpxProcessor.Gpx.trk.name, gpxProcessor.TotalDistanceInMeters, gpxProcessor.TotalElevationInMeters, gpxProcessor.Gpx.metadata.time);
    }
    
    private async void OnNextClicked(object sender, EventArgs e)
    {
        if (RouteListView.SelectedItem is not null)
        {
            var selectedRoute = (dynamic)RouteListView.SelectedItem;
            string selectedGpxFilePath = selectedRoute.FullPath;

            // Pass the selected GPX file path to the RouteDetailsScreen
            await Navigation.PushAsync(new RouteDetailsScreen(selectedGpxFilePath));
        }
    }
 
    private void ForceVideoScaling()
    {
        if (RouteVideo.IsVisible)
        {
            double containerWidth = DetailsGrid.Width; 
            double containerHeight = DetailsGrid.Height;

            double aspectRatio = 9.0 / 16.0;
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

    // Use the displayed video width as the available width for the date label.
    private void AdjustDateFontSize()
    {
        if (DateLabel == null || RouteVideo == null || string.IsNullOrEmpty(DateLabel.Text))
            return;

        // Prefer actual measured width of the video element; fallback to WidthRequest or page estimate
        double available = RouteVideo.Width;
        if (available <= 0) available = RouteVideo.WidthRequest;
        if (available <= 0) available = this.Width * 0.25; // fallback estimate

        // subtract some padding room so label doesn't touch edges
        available -= (DateLabel.Padding.Left + DateLabel.Padding.Right + 12);
        if (available <= 0) return;

        // use a known base font size for measuring
        double baseFont = Math.Max(DateLabel.FontSize, 36d);
        DateLabel.FontSize = baseFont;

        // measure the rendered width at baseFont
        var measured = DateLabel.Measure(double.PositiveInfinity, double.PositiveInfinity);
        double measuredWidth = measured.Width;
        if (measuredWidth <= 0) return;

        // scale font by ratio of available width to measured width
        double scaled = baseFont * (available / measuredWidth);

        // clamp to sensible min/max
        double newFont = Math.Clamp(scaled, 10.0, 48.0);

        if (Math.Abs(DateLabel.FontSize - newFont) > 0.5)
        {
            DateLabel.FontSize = newFont;
            DateLabel.InvalidateMeasure();
            this.InvalidateMeasure();
        }
    }
}
