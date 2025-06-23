namespace Re_RunApp.Core;

using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

internal class GraphPlotter
{

    private GpxProcessor _gpx;
    private int _width = 1920;
    private int _height = 1080;
    private bool _minimal = false;
    public MemoryStream PlotGraph(GpxProcessor gpx, int height = 1080, int width = 1920, bool minimal=false)
    {
        _gpx = gpx;
        _height = height;
        _width = width;
        _minimal = minimal;
        PlotModel plotModel = Plotter(gpx);  
        return StreamPlotModelAsPng(plotModel);
    }
    
    public PlotModel PlotGraph(GpxProcessor gpx, bool minimal = false)
    {
        _gpx = gpx;
        _minimal = minimal;
        return Plotter(gpx);
    }

    public PlotModel RenderDistanceOverlay(decimal meters, decimal nextSegmentLength=0)
    {
        if (_gpx == null)
            throw new InvalidOperationException("No GPX data available to plot.");

        PlotModel plotModel = Plotter(_gpx);
        double maxHeight = plotModel.Axes[1].Maximum;

        var areaSeries = new AreaSeries
        {
            Color = OxyColor.FromAColor(128, OxyColors.LightSkyBlue),
            Fill = OxyColor.FromAColor(128, OxyColors.LightSkyBlue),
            StrokeThickness = 1
        };
        areaSeries.Points.Add(new DataPoint(0, 0));
        areaSeries.Points.Add(new DataPoint(0, maxHeight));
        areaSeries.Points.Add(new DataPoint((double)meters, maxHeight));
        areaSeries.Points.Add(new DataPoint((double)meters, 0));
        plotModel.Series.Add(areaSeries);

        if (nextSegmentLength > 0)
        {
            var nextSeries = new AreaSeries
            {
                Color = OxyColor.FromAColor(255, OxyColors.OrangeRed),
                Fill = OxyColor.FromAColor(32, OxyColors.Black),
                StrokeThickness = 1
            };
            nextSeries.Points.Add(new DataPoint((double)meters, 0));
            nextSeries.Points.Add(new DataPoint((double)meters, maxHeight));
            nextSeries.Points.Add(new DataPoint((double)(meters + nextSegmentLength), maxHeight));
            nextSeries.Points.Add(new DataPoint((double)(meters + nextSegmentLength), 0));
            plotModel.Series.Add(nextSeries);
        }

        var lineSeries = new LineSeries
        {
            Color = OxyColors.OrangeRed,
            StrokeThickness = 3
        };
        lineSeries.Points.Add(new DataPoint((double)meters, 0));
        lineSeries.Points.Add(new DataPoint((double)meters, maxHeight));
        plotModel.Series.Add(lineSeries);

        return plotModel;
    }



    private PlotModel Plotter(GpxProcessor gpx)
    {
        var plotModel = new PlotModel();
        if (!_minimal) plotModel.Title = "Re-Run Virtual Run";

        double maxDistance = (double)(gpx.Tracks.Last().TotalDistanceInMeters / 1000) * 1000;
        if (maxDistance < 1000) maxDistance = 1000;

        double minElevation = gpx.FindMinimumElevation();
        double maxElevation = gpx.FindMaximumElevation();
        int elevationScale = maxElevation > 100 ? 100 : 10;

        plotModel.Background = OxyColor.FromArgb(128,0xFF,0xFF,0xFF);

        plotModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = 0,
            Maximum = maxDistance,
            MajorStep = 1000,
            Title = (!_minimal) ? "Distance (meters)": "Distance (M)",
            FontSize = 16
        });

        plotModel.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Minimum = (double)(minElevation / 10 * 10),
            Maximum = (double)(maxElevation + elevationScale),
            MajorStep = (!_minimal)? elevationScale: double.NaN,
            Title = (!_minimal) ?  "Height (meters)": "Height (M)",
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

        return plotModel;
    }


    public MemoryStream RenderDistanceOverlay(decimal meters, bool bas=false)
    {
        if (_gpx == null)
            throw new InvalidOperationException("No GPX data available to plot.");

        PlotModel plotModel = Plotter(_gpx);
        double maxHeight = plotModel.Axes[1].Maximum;
      
        var areaSeries = new AreaSeries
        {
            Color = OxyColor.FromAColor(128, OxyColors.LightSkyBlue),
            Fill = OxyColor.FromAColor(128, OxyColors.LightSkyBlue),
            StrokeThickness = 1
        };
        areaSeries.Points.Add(new DataPoint(0, 0));
        areaSeries.Points.Add(new DataPoint(0, maxHeight));
        areaSeries.Points.Add(new DataPoint((double)meters, maxHeight));
        areaSeries.Points.Add(new DataPoint((double)meters, 0));
        plotModel.Series.Add(areaSeries);

        var lineSeries = new LineSeries
        {
            Color = OxyColors.OrangeRed,
            StrokeThickness = 4
        };
        lineSeries.Points.Add(new DataPoint((double)meters, 0));
        lineSeries.Points.Add(new DataPoint((double)meters, maxHeight));
        plotModel.Series.Add(lineSeries);

        return StreamPlotModelAsPng(plotModel);
    }


    private MemoryStream StreamPlotModelAsPng(PlotModel plotModel)
    {
        try
        {
            MemoryStream stream = new MemoryStream();
            //var pngExporter = new PngExporter { Width = _width, Height = _height };
            //pngExporter.Export(plotModel, stream);
            //stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error exporting plot model to PNG: {ex.Message}");
            return new MemoryStream();
        }
    }

    private OxyColor GetColorForPercentage(decimal percentage)
    {
        percentage = Math.Round(percentage, 0);

        if (percentage < 0)
            return OxyColors.Blue;
        if (percentage <=5)
            return OxyColors.Green;
        if (percentage <= 6)
            return OxyColors.Yellow;
        if (percentage <= 10)
            return OxyColors.Orange;
        if (percentage <= 12)
            return OxyColors.Red;
        return OxyColors.DarkRed;
    }

}

