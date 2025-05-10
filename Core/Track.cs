namespace Re_RunApp.Core;

public class Track
{
    public int Index { get; set; }
    public int GpxLastIndex { get; set; }
    public decimal DistanceInMeters { get; set; }
    public decimal AscendInMeters { get; set; }
    public decimal DescendInMeters { get; set; }

    public decimal InclinationInDegrees { get; set; }
    public decimal TotalDistanceInMeters { get; set; }
    public decimal TrackStartDistanceInMeters { get; set; }
    public bool PreviousTrackDescended { get; set; }

    public decimal StartElevation { get; set; }
    public decimal EndElevation { get; set; }

    public int HeartRate { get; set; } = 0;
    public List<TrackSegment> Segments { get; set; } = [];
}
