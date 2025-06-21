namespace Re_RunApp.Views;

using CommunityToolkit.Maui.Views;
using Re_RunApp.Core;

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

            TitleLabel.Text = $"Title: {routeDetails.Title}";
            DistanceLabel.Text = $"Distance: {routeDetails.Distance / 1000:F1} km";
            ElevationLabel.Text = $"Elevation: {routeDetails.Elevation:F0} m";

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

    private (string Title, decimal Distance, decimal Elevation) GetRouteDetails(string fullPath)
    {
        var gpxProcessor = new GpxProcessor();
        gpxProcessor.LoadGpxData(fullPath);
        gpxProcessor.GetRun();

        return (gpxProcessor.Gpx.trk.name, gpxProcessor.TotalDistanceInMeters, gpxProcessor.TotalElevationInMeters);
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
