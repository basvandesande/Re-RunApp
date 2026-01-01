namespace Re_RunApp.Core;

public class RunSettings
{
    public double Speed0to5 { get; set; }
    public double Speed6to8 { get; set; }
    public double Speed8to10 { get; set; }
    public double Speed11to12 { get; set; }
    public double Speed13to15 { get; set; }
    public bool AutoSpeedControl { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Favourite { get; set; }
    public Intensity Level { get; set; } = Intensity.Moderate;
    public decimal TotalDistance { get; set; } = 0;
    public decimal TotalAscend { get; set; } = 0;
}


public enum Intensity
{
    Easy = 0 ,
    Moderate = 1,
    Hard = 2,
    VeryHard = 3,
    Max = 4
}