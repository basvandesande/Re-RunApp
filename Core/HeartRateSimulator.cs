using System.Timers;

namespace Re_RunApp.Core;

public class HeartRateSimulator : IHeartRate
{
    public event Action<int>? OnHeartPulse;

    public bool Enabled { get; set; } = true;
    public int CurrentRate { get; private set; } = 70;

    private System.Timers.Timer? _timer; // Fully qualify the Timer type
    private readonly Random _random = new();

    public Task<bool> ConnectToDevice(bool showDialog = true)
    {
        //if (!Enabled)
        //    return Task.FromResult(false);

        _timer = new System.Timers.Timer(1000); // Fully qualify the Timer type
        _timer.Elapsed += (s, e) =>
        {
            // Simulate heart rate between 123 and 160 bpm
            CurrentRate = _random.Next(123, 140);
            OnHeartPulse?.Invoke(CurrentRate);
        };
        _timer.Start();

        return Task.FromResult(true);
    }

    public void Disconnect()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }
}
