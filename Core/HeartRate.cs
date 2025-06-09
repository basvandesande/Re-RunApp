namespace Re_RunApp.Core;

public class HeartRate: IHeartRate
{
    public string Name => _device?.Name ?? string.Empty;

    private readonly string _deviceIdFile = Path.Combine(Runtime.GetAppFolder(), "heartrate_device_id.txt");
    private readonly Guid _optionalService = Guid.Parse("0000180D-0000-1000-8000-00805F9B34FB"); // Heart Rate Service UUID
    private readonly Guid _measureCharacteristic = Guid.Parse("00002A37-0000-1000-8000-00805F9B34FB");

    InTheHand.Bluetooth.BluetoothDevice? _device = null;

    public bool Enabled { get; set; } = true;

    public int CurrentRate { get; private set; }


    public event Action<int>? OnHeartPulse;


    internal void DeleteDeviceIdFile()
    {
        Runtime.DeleteDeviceIdFile(_deviceIdFile);
    }



    public async Task<bool> ConnectToDevice(bool showDialog = true)
    {
        if (!Enabled) return false;

        _device = await Runtime.GetOrRequestDeviceAsync(_deviceIdFile, _optionalService, showDialog);

        if (_device != null && _device.Gatt.IsConnected)
        {
            // Get the Heart Rate Service
            var heartRateService = await _device.Gatt.GetPrimaryServiceAsync(_optionalService);
            if (heartRateService != null)
            {
                // Get the Heart Rate Measurement characteristic
                var heartRateCharacteristic = await heartRateService.GetCharacteristicAsync(_measureCharacteristic); // Heart Rate Measurement UUID
                if (heartRateCharacteristic != null)
                {

                    Enabled = true;
                    heartRateCharacteristic.CharacteristicValueChanged += (sender, args) =>
                    {
                        var data = args.Value;
                        if (data != null && data.Length > 1)
                        {
                            // Parse the heart rate value (first byte contains flags, second byte contains the heart rate)
                            CurrentRate = data[1];

                            // raise new event....
                            OnHeartPulse?.Invoke(CurrentRate);
                        }
                    };
                    await heartRateCharacteristic.StartNotificationsAsync();
                }
                else
                {
                    Enabled = false;
                    return false;
                }
            }
        }
        return true;
    }

    public void Disconnect()
    {
        try
        {

            // Disconnect and dispose the device
            if (_device != null && _device.Gatt.IsConnected)
            {
                _device.Gatt.Disconnect();
            }
            _device = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during Heartrate disconnect: {ex.Message}");
        }
    }
}
