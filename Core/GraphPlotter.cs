namespace Re_RunApp.Core;

using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.SkiaSharp;

internal class GraphPlotter
{
    public Stream PlotGraph(GpxProcessor gpx)
    {
        double maxDistance = (double)(gpx.Tracks.Last().TotalDistanceInMeters / 1000) * 1000;
        if (maxDistance < 1000) maxDistance = 1000;

        double minElevation = gpx.FindMinimumElevation();
        double maxElevation = gpx.FindMaximumElevation();
        int elevationScale = maxElevation > 100 ? 100 : 10;

        var plotModel = new PlotModel { Title = "Re-Run Virtual Run" };
        plotModel.Background = OxyColors.White;

        Random random = new Random();
        int randomIndex = random.Next(0, gpx.Tracks.Length - 1);

        plotModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = 0,
            Maximum = maxDistance,
            MajorStep = 1000,
            Title = "Distance (meters)",
            FontSize = 20
        });

        plotModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Minimum = (double)(minElevation / 10 * 10),
            Maximum = (double)(maxElevation + elevationScale),
            MajorStep = elevationScale,
            Title = "Height (meters)",
            FontSize = 20
        });

        var areaSeries = new AreaSeries
        {
            Title = "Elevation",
            Color = OxyColors.Green,
            Fill = OxyColors.LightGreen,
            StrokeThickness = 10
        };

        foreach (var track in gpx.Tracks)
        {
            areaSeries.Points.Add(new DataPoint((double)track.TotalDistanceInMeters, (double)track.EndElevation));
        }
        plotModel.Series.Add(areaSeries);

        for (int i = 1000; i <= (int)maxDistance; i += 1000)
        {
            var verticalLine = new LineAnnotation
            {
                Type = LineAnnotationType.Vertical,
                X = i,
                Color = OxyColors.Gray,
                LineStyle = LineStyle.Dash
            };
            plotModel.Annotations.Add(verticalLine);
        }

        // to do: not needed for output image
        // Create a scatter series for the red point
        //var scatterSeries = new ScatterSeries
        //{
        //    MarkerType = MarkerType.Diamond,
        //    MarkerStroke = OxyColors.Red,
        //    MarkerFill = OxyColors.Orange,
        //    MarkerSize = 10
        //};

        //// Add the red point  
        //// TODO: make this a real point not a random one
        //scatterSeries.Points.Add(new ScatterPoint((double)gpx.Tracks[randomIndex].TotalDistanceInMeters, 
        //                                          (double)gpx.Tracks[randomIndex].EndElevation));
        //plotModel.Series.Add(scatterSeries);

        
        var stream = new MemoryStream();
        var pngExporter = new PngExporter { Width = 1920, Height = 1080 };
        pngExporter.Export(plotModel, stream);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }
}

