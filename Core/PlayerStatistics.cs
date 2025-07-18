namespace Re_RunApp.Core;

public class PlayerStatistics
{
    public decimal? CurrentSpeedKMH { get; set; } = 0;
    public decimal? CurrentDistanceM { get; set; } = 0;
    public decimal? SegmentRemainingM { get; set; } = 0;
    public decimal? SegmentIncrementPercentage { get; set; } = 0;
    public double SecondsElapsed { get; set; } = 0;
    public decimal? TotalInclinationM { get; set; } = 0;
    public decimal? TotalDeclinationM { get; set; } = 0;
    public int? CurrentHeartRate { get; set; } = 0;
    public List<int> HeartRateSamples { get; } = new();


    public TimeSpan? CurrentSpeedMinKM => CurrentSpeedKMH.HasValue && CurrentSpeedKMH > 0
                                            ? TimeSpan.FromMinutes(60 / (double)CurrentSpeedKMH)
                                            : null;

}
