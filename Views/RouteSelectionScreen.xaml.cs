namespace Re_RunApp.Views;

using CommunityToolkit.Maui.Views;
using Re_RunApp.Core;
using Windows.Media.Audio;

public partial class RouteSelectionScreen : ContentPage
{
    public RouteSelectionScreen()
    {
        InitializeComponent();
        this.Loaded += RouteSelectionScreen_Loaded;

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
        if (e.SelectedItem is not null)
        {
            if (!this.IsLoaded)
            {
                OnRouteSelected(sender, e);
            }
            var selectedRoute = (dynamic)e.SelectedItem;
            string fullPath = selectedRoute.FullPath;

            var routeDetails = GetRouteDetails(fullPath);

            TitleLabel.Text = routeDetails.Title;
            DistanceLabel.Text = $"{routeDetails.Distance / 1000:F1}";
            ElevationLabel.Text = $"{routeDetails.Elevation:F0}";
            DateLabel.Text = $"{routeDetails.Date:ddd, d MMMM yyyy}";

            string videoPath = Path.ChangeExtension(fullPath, ".mp4");
            if (File.Exists(videoPath))
            {
                RouteVideo.Source = MediaSource.FromFile(videoPath);
            }
            else
            {
                RouteVideo.Source = MediaSource.FromResource("nomedia.mp4");
            }
            ForceVideoScaling();

        }

        NextButton.IsEnabled = (e.SelectedItem is not null);
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


}
