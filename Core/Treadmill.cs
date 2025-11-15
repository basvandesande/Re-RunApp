
namespace Re_RunApp.Core;

using InTheHand.Bluetooth;

internal class Treadmill: ITreadmill
{
    public string Name => _device?.Name ?? string.Empty;

    private BluetoothDevice? _device;
    private GattCharacteristic? _characteristic;
    private GattCharacteristic? _statusCharacteristic;
    private GattCharacteristic? _activityCharacteristic;
    private GattCharacteristic? _supportedSpeedRangeCharacteristic;
    private GattCharacteristic? _supportedInclinationRangeCharacteristic;

    private readonly string _deviceIdFile = Path.Combine(Runtime.GetAppFolder(),"treadmill_device_id.txt");
    private readonly Guid _treadmillServiceId = Guid.Parse("00001826-0000-1000-8000-00805f9b34fb");
    private readonly Guid _controlPointId = Guid.Parse("00002ad9-0000-1000-8000-00805f9b34fb");
    private readonly Guid _statusId = Guid.Parse("00002acd-0000-1000-8000-00805f9b34fb");
    private readonly Guid _activityId = Guid.Parse("00002ada-0000-1000-8000-00805f9b34fb");
    private readonly Guid _SupportedSpeedRangeId = Guid.Parse("00002ad4-0000-1000-8000-00805f9b34fb");
    private readonly Guid _SupportedInclinationRangeId = Guid.Parse("00002ad5-0000-1000-8000-00805f9b34fb");


    public event Action<TreadmillStatistics>? OnStatisticsUpdate;
    public event Action<string>? OnStatusUpdate;
    

    private decimal _minInclination = 0;
    private decimal _maxInclination = 0;
    //private decimal _inclineResolution = 1;

    private decimal _minSpeed = 0;
    private decimal _maxSpeed = 0;
    //private decimal _speedResolution = 0.1m;

    internal void DeleteDeviceIdFile()
    {
        Runtime.DeleteDeviceIdFile(_deviceIdFile);
    }


    public async Task<bool> ConnectToDevice(bool showDialog=true)
    {
        try
        {
            _device = await Runtime.GetOrRequestDeviceAsync(_deviceIdFile, _treadmillServiceId, showDialog);
            if (_device != null && _device.Gatt.IsConnected)
            {
                GattService service = await _device.Gatt.GetPrimaryServiceAsync(_treadmillServiceId);
                _characteristic = await service.GetCharacteristicAsync(_controlPointId);
                _statusCharacteristic = await service.GetCharacteristicAsync(_statusId);
                _activityCharacteristic = await service.GetCharacteristicAsync(_activityId);
                _supportedSpeedRangeCharacteristic = await service.GetCharacteristicAsync(_SupportedSpeedRangeId);
                _supportedInclinationRangeCharacteristic = await service.GetCharacteristicAsync(_SupportedInclinationRangeId);

                _activityCharacteristic.CharacteristicValueChanged += HandleActivityNotifications;
                await _activityCharacteristic.StartNotificationsAsync();

                _characteristic.CharacteristicValueChanged += HandleRequestNotifications;
                await _characteristic.StartNotificationsAsync();

                _statusCharacteristic.CharacteristicValueChanged += HandleStatusNotifications;
                await _statusCharacteristic.StartNotificationsAsync();

                await ReadSupportedInclinationRangeAsync();
                await ReadSupportedSpeedRangeAsync();

                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to device: {ex.Message}");
            await ResetAsync();
        }
        return false;
    }

    private void HandleActivityNotifications(object? sender, GattCharacteristicValueChangedEventArgs args)
    {
        var bytes = args.Value;
        if (bytes != null)
        {
            string output = string.Empty;
            // check bytes
            switch (bytes[0])
            {
                case 0x01: // Reset
                    output = "RESET";
                    break;
                case 0x02: // Stop pressed
                    output = "STOP";
                    break;
                case 0x03: // Stop security key
                    output = "STOP KEY";
                    break;
                case 0x04: // Start pressed / Resume
                    output = "START";
                    break;
                default:
                    return; // Ignore other notifications
            }
            OnStatusUpdate?.Invoke(output);
        }
    }

    private void HandleRequestNotifications(object? sender, GattCharacteristicValueChangedEventArgs args)
    {
        var bytes = args.Value;
        if (bytes != null)
        {
            var hexStringBuilder = new System.Text.StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                hexStringBuilder.AppendFormat("{0:x2} ", b);
            //Console.WriteLine(hexStringBuilder.ToString());
            OnStatusUpdate?.Invoke(hexStringBuilder.ToString());
        }
    }

    private void HandleStatusNotifications(object? sender, GattCharacteristicValueChangedEventArgs args)
    {
        if (args.Value == null) return;
        byte[] bytes = args.Value;

        List<string> a = new List<string>();
        for (int i = 0; i < bytes.Length; i++)
            a.Add("0x" + bytes[i].ToString("X2"));

        // check what bytes is available: in byte 0, the flags are stored
        ushort flags = BitConverter.ToUInt16(bytes, 0);
        int nextPosition = 4;

        int? posAvgSpeed = null, posTotDistance = null, posInclination = null, posElevGain = null;
        int? posInsPace = null, posAvgPace = null, posKcal = null, posHR = null;
        int? posMET = null, posElapsedTime = null, posRemainTime = null, posForceBelt = null;

        if ((flags & (1 << 1)) != 0) { posAvgSpeed = nextPosition; nextPosition += 2; }
        if ((flags & (1 << 2)) != 0) { posTotDistance = nextPosition; nextPosition += 3; }
        if ((flags & (1 << 3)) != 0) { posInclination = nextPosition; nextPosition += 4; }
        if ((flags & (1 << 4)) != 0) { posElevGain = nextPosition; nextPosition += 4; }
        if ((flags & (1 << 5)) != 0) { posInsPace = nextPosition; nextPosition += 1; }
        if ((flags & (1 << 6)) != 0) { posAvgPace = nextPosition; nextPosition += 1; }
        if ((flags & (1 << 7)) != 0) { posKcal = nextPosition; nextPosition += 5; }
        if ((flags & (1 << 8)) != 0) { posHR = nextPosition; nextPosition += 1; }
        if ((flags & (1 << 9)) != 0) { posMET = nextPosition; nextPosition += 1; }
        if ((flags & (1 << 10)) != 0) { posElapsedTime = nextPosition; nextPosition += 2; }
        if ((flags & (1 << 11)) != 0) { posRemainTime = nextPosition; nextPosition += 2; }
        if ((flags & (1 << 12)) != 0) { posForceBelt = nextPosition; nextPosition += 4; }

        TreadmillStatistics stats = new()
        {
            SpeedKMH = BitConverter.ToUInt16(bytes, 2) / 100.0m,
            DistanceM = posTotDistance.HasValue ? BitConverter.ToUInt16(bytes, posTotDistance.Value) : null,
            InclinationPercentage = posInclination.HasValue ? (decimal)BitConverter.ToInt16(bytes, posInclination.Value) / 10.0m : null,
            kcal = posKcal.HasValue ? BitConverter.ToUInt16(bytes, posKcal.Value) : null,
            HeartRate = posHR.HasValue ? bytes[posHR.Value] : null,
            ElapsedSeconds = posElapsedTime.HasValue ? BitConverter.ToUInt16(bytes, posElapsedTime.Value) : null
        };

        OnStatisticsUpdate?.Invoke(stats);
    }

    public void Disconnect()
    {
        try
        {
         
            // Dispose GATT objects
            _activityCharacteristic = null;
            _characteristic = null;
            _statusCharacteristic = null;
            _supportedSpeedRangeCharacteristic = null;
            _supportedInclinationRangeCharacteristic = null;

            // Disconnect and dispose the device
            if (_device != null && _device.Gatt.IsConnected)
            {
                _device.Gatt.Disconnect();
            }
            _device = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during Treadmill disconnect: {ex.Message}");
        }
    }

    public async Task StartAsync()
    {
        if (_characteristic != null)
        {
            await ResetAsync();
            await _characteristic.WriteValueWithResponseAsync([0x07]);

            // set initial speed to 5 km/h
            await Task.Delay(1000);
            await ChangeSpeedAsync(5);
        }
    }

    public async Task StopAsync()
    {
        if (_characteristic != null)
        {
            await _characteristic.WriteValueWithResponseAsync([0x08]);
            await ResetAsync();
        }
    }

    public async Task ResetAsync()
    {
        if (_characteristic != null)
            await _characteristic.WriteValueWithResponseAsync([0x01]);
    }


    public async Task ChangeSpeedAsync(decimal speed)
    {
        // speed change 02 C8 00n
        // 02 – let the treadmill know you are sending it a speed change request
        // C8 = 200 = 2kmh at resolution of 0.01kmh  
        // 00 – spacer
        if (_characteristic != null && _maxSpeed > 0)
        {
            // protect the treadmill, don't oversteer it
            if (speed < _minSpeed) speed = _minSpeed;
            if (speed > _maxSpeed) speed = _maxSpeed;

            // change resolution of the speed and convert to bytes
            speed *= 100;
            byte[] speedBytes = BitConverter.GetBytes((short)speed);

            await _characteristic.WriteValueWithResponseAsync(new byte[] { 0x02, speedBytes[0], speedBytes[1] });
        }
    }

    public async Task ChangeInclineAsync(short increment)
    {
        if (_characteristic != null)
        {
            // protect the treadmill, don't oversteer it
            if (increment < _minInclination) increment = (short)_minInclination;
            if (increment > _maxInclination) increment = (short)_maxInclination;

            increment *= 10;
            byte[] incrementBytes = BitConverter.GetBytes(increment);

            byte[] command = [0x03, incrementBytes[0], incrementBytes[1]];
            await _characteristic.WriteValueWithResponseAsync(command);
        }
    }

    private async Task ReadSupportedInclinationRangeAsync()
    {
        if (_supportedInclinationRangeCharacteristic != null)
        {
            var bytes = await _supportedInclinationRangeCharacteristic.ReadValueAsync();
            if (bytes != null && bytes.Length >= 6)
            {
                _minInclination = BitConverter.ToInt16(bytes, 0) / 10.0m; // Min inclination in %
                _maxInclination = BitConverter.ToInt16(bytes, 2) / 10.0m; // Max inclination in %
                //_inclineResolution = BitConverter.ToInt16(bytes, 4) / 10.0m; // Resolution in %
            }    
        }
    }
    private async Task ReadSupportedSpeedRangeAsync()
    {
        if (_supportedSpeedRangeCharacteristic != null)
        {
            var bytes = await _supportedSpeedRangeCharacteristic.ReadValueAsync();
            if (bytes != null && bytes.Length >= 6)
            {
                _minSpeed = BitConverter.ToUInt16(bytes, 0) / 100.0m; // Min speed in km/h
                _maxSpeed = BitConverter.ToUInt16(bytes, 2) / 100.0m; // Max speed in km/h
                //_speedResolution = BitConverter.ToUInt16(bytes, 4) / 100.0m; // Resolution in km/h
            }
        }
    }
}
