namespace Re_RunApp.Core;

internal class PlayerStatistics
{
    public decimal? CurrentSpeedKMH { get; set; } = 0;
    public TimeSpan? CurrentSpeedMinKM { get; set; }
    public decimal? TotalDistanceM { get; set; } = 0;
    public decimal? SegmentRemainingM { get; set; } = 0;
    public decimal? SegmentIncrementPercentage { get; set; } = 0;
    public double SecondsElapsed { get; set; } = 0;
    public decimal? TotalInclinationM { get; set; } = 0;
    public decimal? TotalDeclinationM { get; set; } = 0;
    public int? CurrentHeartRate { get; set; } = 0;
}
