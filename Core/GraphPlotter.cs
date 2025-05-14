namespace Re_RunApp.Core;

using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.SkiaSharp;

internal class GraphPlotter
{
    public Stream PlotGraph(GpxProcessor gpx, int height=1080, int width=1920)
    {
        double maxDistance = (double)(gpx.Tracks.Last().TotalDistanceInMeters / 1000) * 1000;
        if (maxDistance < 1000) maxDistance = 1000;

        double minElevation = gpx.FindMinimumElevation();
        double maxElevation = gpx.FindMaximumElevation();
        int elevationScale = maxElevation > 100 ? 100 : 10;

        var plotModel = new PlotModel { Title = "Re-Run Virtual Run" };
        plotModel.Background = OxyColors.LightBlue;

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
            Color = OxyColors.White,
            Fill = OxyColors.SandyBrown,
            StrokeThickness = 10
        };

        foreach (var track in gpx.Tracks)
        {
            areaSeries.Color = GetColorForPercentage(track.InclinationInDegrees);
            areaSeries.Points.Add(new DataPoint((double)track.TotalDistanceInMeters, (double)track.EndElevation));
       
        // todo fix coloring
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

        
        var stream = new MemoryStream();
        var pngExporter = new PngExporter { Width = width, Height = height };
        pngExporter.Export(plotModel, stream);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private OxyColor GetColorForPercentage(decimal percentage)
    {
        if (percentage < 5)
            return OxyColors.White;
        else if (percentage < 8)
            return OxyColors.Yellow;
        else if (percentage < 10)
            return OxyColors.Orange;
        else if (percentage < 12)
            return OxyColors.Red;
        else
            return OxyColors.DarkRed;
    }

}

