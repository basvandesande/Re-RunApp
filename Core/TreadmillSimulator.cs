namespace Re_RunApp.Core;
public class TreadmillSimulator : ITreadmill
{
    public event Action<TreadmillStatistics>? OnStatisticsUpdate;
    public event Action<string>? OnStatusUpdate;

    private bool _isRunning;
    private CancellationTokenSource _cts;
    private decimal _percentageIncline=0;
    private decimal _speed = 0;

    public Task StartAsync()
    {
        _isRunning = true;
        _cts = new CancellationTokenSource();
        SimulateAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _isRunning = false;
        _cts?.Cancel();
        OnStatusUpdate?.Invoke("STOP");
        return Task.CompletedTask;
    }

    public Task ResetAsync() => Task.CompletedTask;

    public Task ChangeInclineAsync(short increment)
    {
        _percentageIncline = increment;
        return Task.CompletedTask;
    }

    public Task ChangeSpeedAsync(decimal speed)
    {
        _speed = speed;
        return Task.CompletedTask;
    }

    private async void SimulateAsync(CancellationToken token)
    {
        double distance = 0;
        
        while (_isRunning && !token.IsCancellationRequested)
        {
            distance += (double)_speed * 1000 / 3600 ; 
            var stats = new TreadmillStatistics
            {
                SpeedKMH = _speed,
                DistanceM = distance,
                InclinationPercentage = _percentageIncline,
                HeartRate = 120,
                ElapsedSeconds = (int)(distance / ((double)_speed * 1000 / 3600))
            };
            OnStatisticsUpdate?.Invoke(stats);
            await Task.Delay(1000, token);
        }
    }
}
