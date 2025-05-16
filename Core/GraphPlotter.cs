namespace Re_RunApp.Core;

using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.SkiaSharp;

internal class GraphPlotter
{
    public Stream PlotGraph(GpxProcessor gpx, int height = 1080, int width = 1920)
    {
        double maxDistance = (double)(gpx.Tracks.Last().TotalDistanceInMeters / 1000) * 1000;
        if (maxDistance < 1000) maxDistance = 1000;

        double minElevation = gpx.FindMinimumElevation();
        double maxElevation = gpx.FindMaximumElevation();
        int elevationScale = maxElevation > 100 ? 100 : 10;

        var plotModel = new PlotModel { Title = "Re-Run Virtual Run" };
        plotModel.Background = OxyColors.White;

        plotModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = 0,
            Maximum = maxDistance,
            MajorStep = 1000,
            Title = "Distance (meters)",
            FontSize = 16
        });

        plotModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Minimum = (double)(minElevation / 10 * 10),
            Maximum = (double)(maxElevation + elevationScale),
            MajorStep = elevationScale,
            Title = "Height (meters)",
            FontSize = 16
        });

        if (gpx.Tracks.Length > 0)
        {
            double prevDistance = 0;
            double prevElevation = (double)gpx.Tracks[0].StartElevation;

            for (int i = 0; i < gpx.Tracks.Length; i++)
            {
                var track = gpx.Tracks[i];
                double endDistance = (double)track.TotalDistanceInMeters;
                double endElevation = (double)track.EndElevation;
                var color = GetColorForPercentage(track.InclinationInDegrees);

                
                var lineSeries = new LineSeries
                {
                    Color = color,
                    StrokeThickness = 5
                };

                var areaSeries = new AreaSeries
                {
                    Color = OxyColors.Black,
                    Fill = OxyColors.LightGray,
                    StrokeThickness = 1
                };

                // Top line (elevation)
                areaSeries.Points.Add(new DataPoint(prevDistance, prevElevation));
                areaSeries.Points.Add(new DataPoint(endDistance, endElevation));
                
                plotModel.Series.Add(areaSeries);

                lineSeries.Points.Add(new DataPoint(prevDistance, prevElevation));
                lineSeries.Points.Add(new DataPoint(endDistance, endElevation));
                plotModel.Series.Add(lineSeries);

                prevDistance = endDistance;
                prevElevation = endElevation;
            }
        }

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

        // Legend settings
        double legendY = (double)(maxElevation + elevationScale) - 0.06 * (maxElevation - minElevation); // 5% from top
        double legendXStart = maxDistance * 0.45; // Start legend at 45% of width
        double boxWidth = maxDistance * 0.03;     // Width of color box
        double boxHeight = (maxElevation - minElevation) * 0.05; // Height of color box
        double spacing = maxDistance * 0.016;      // Space between items

        var legendItems = new (OxyColor color, string label)[]
        {
            (OxyColors.Blue,  "< 0"),
            (OxyColors.Green, "0-5"),
            (OxyColors.Yellow, "6-8"),
            (OxyColors.Orange, "8-10"),
            (OxyColors.Red, "11-12"),
            (OxyColors.DarkRed, "13-15 %"),
        };

        double x = legendXStart;
        foreach (var legendItem in legendItems)
        {
            // Color box
            plotModel.Annotations.Add(new RectangleAnnotation
            {
                MinimumX = x + 40,
                MaximumX = x + 35 + boxWidth,
                MinimumY = legendY,
                MaximumY = legendY + boxHeight,
                Fill = legendItem.color,
                Stroke = OxyColors.Black,
                StrokeThickness = 1,
                Layer = AnnotationLayer.AboveSeries
            });

            // Text next to box
            plotModel.Annotations.Add(new TextAnnotation
            {
                Text = legendItem.label,
                TextPosition = new DataPoint(x + boxWidth + spacing - 15, legendY + boxHeight / 2),
                FontSize = 14,
                Stroke = OxyColors.Undefined,
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Middle,
                Layer = AnnotationLayer.AboveSeries
            });

            x += boxWidth + spacing * 3; // Move to the right for next item
        }

        var stream = new MemoryStream();
        var pngExporter = new PngExporter { Width = width, Height = height };
        pngExporter.Export(plotModel, stream);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }


    private OxyColor GetColorForPercentage(decimal percentage)
    {
        if (percentage < 0)
            return OxyColors.Blue;
        if (percentage < 5)
            return OxyColors.Green;
        if (percentage < 8)
            return OxyColors.Yellow;
        if (percentage < 10)
            return OxyColors.Orange;
        if (percentage < 12)
            return OxyColors.Red;
        return OxyColors.DarkRed;
    }

}

