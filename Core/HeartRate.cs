using InTheHand.Bluetooth;

namespace Re_RunApp.Core;

public class HeartRate: IHeartRate
{
    public string Name => _device?.Name ?? string.Empty;

    private readonly string _deviceIdFile = Path.Combine(Runtime.GetAppFolder(), "heartrate_device_id.txt");
    private GattCharacteristic? _measureCharacteristic;

    private readonly Guid _heartRateServiceId = Guid.Parse("0000180D-0000-1000-8000-00805F9B34FB"); // Heart Rate Service UUID
    private readonly Guid _measureCharacteristicId = Guid.Parse("00002A37-0000-1000-8000-00805F9B34FB");

    private DateTime _lastPulseSent = DateTime.MinValue;
    private static readonly TimeSpan PulseThrottleInterval = TimeSpan.FromSeconds(5);

    private InTheHand.Bluetooth.BluetoothDevice? _device = null;

    public bool Enabled { get; set; } = true;

    public int CurrentRate { get; private set; }


    public event Action<int>? OnHeartPulse;


    internal void DeleteDeviceIdFile()
    {
        Runtime.DeleteDeviceIdFile(_deviceIdFile);
    }



    public async Task<bool> ConnectToDevice(bool showDialog = true)
    {
        try
        {
            //if (!Enabled) return false;

            _device = await Runtime.GetOrRequestDeviceAsync(_deviceIdFile, _heartRateServiceId, showDialog);
            if (_device == null)
            {
                Console.WriteLine("No device selected or connection failed.");
                return false;
            }

            // Connect to the device
            if (!_device.Gatt.IsConnected)
            {
                await _device.Gatt.ConnectAsync();
            }


            if (_device.Gatt.IsConnected)
            {
                var heartRateService = await _device.Gatt.GetPrimaryServiceAsync(_heartRateServiceId);
                if (heartRateService == null)
                {
                    Console.WriteLine("Heart Rate Service not found on the device.");
                    return false;
                }

                _measureCharacteristic = await heartRateService.GetCharacteristicAsync(_measureCharacteristicId);
                if (_measureCharacteristic == null)
                {
                    Console.WriteLine("Heart Rate Measurement characteristic not found.");
                    return false;
                }

                Enabled = true;
                _measureCharacteristic.CharacteristicValueChanged += HandleMeasureNotifications;
                await _measureCharacteristic.StartNotificationsAsync();
                return true;
            }
            else
            {
                Console.WriteLine("Device is not connected.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during device connection: {ex.Message}");
            return false;
        }
    }

    private void HandleMeasureNotifications(object? sender, GattCharacteristicValueChangedEventArgs args)
    {
        var data = args.Value;
        if (data != null && data.Length > 1)
        {
            CurrentRate = data[1];

            var now = DateTime.UtcNow;
            if ((now - _lastPulseSent) >= PulseThrottleInterval)
            {
                _lastPulseSent = now;
                OnHeartPulse?.Invoke(CurrentRate);
            }
        }
    }

    public void Disconnect()
    {
        try
        {
            _measureCharacteristic = null;

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
