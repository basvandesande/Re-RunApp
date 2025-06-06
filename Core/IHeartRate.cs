namespace Re_RunApp.Core;

public interface IHeartRate
{
    event Action<int>? OnHeartPulse;
    bool Enabled { get; set; }
    int CurrentRate { get; }
    Task<bool> ConnectToDevice(bool showDialog = true);
    void Disconnect();
}
