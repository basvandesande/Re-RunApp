namespace Re_RunApp.Core;
public interface ITreadmill
{
    event Action<TreadmillStatistics> OnStatisticsUpdate;
    event Action<string> OnStatusUpdate;
    Task StartAsync();
    Task StopAsync();
    Task ResetAsync();
    Task ChangeInclineAsync(short increment);
    Task ChangeSpeedAsync(decimal speed);


}
