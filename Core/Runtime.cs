namespace Re_RunApp.Core;

using InTheHand.Bluetooth;

internal class Runtime
{
    internal static Treadmill Treadmill { get; set; } = new Treadmill();
    internal static TreadmillSimulator TreadmillSimulator { get; set; } = new TreadmillSimulator();
    internal static HeartRate HeartRate { get; set; } = new HeartRate();
    internal static HeartRateSimulator HeartRateSimulator { get; set; } = new HeartRateSimulator();

    internal static RunSettings? RunSettings { get; set; }
    internal static StravaSettings? StravaSettings { get; set; }

    // Load Strava settings at startup
    static Runtime()
    {
        StravaSettings = ConfigurationManager.LoadStravaSettings();
    }

    public static void DeleteDeviceIdFile(string deviceIdFile)
    {
        if (File.Exists(deviceIdFile))
        {
            File.Delete(deviceIdFile);
        }
    }


    public static async Task<BluetoothDevice?> GetOrRequestDeviceAsync(string deviceIdFile, Guid optionalService , bool showDialog=true)
    {
        BluetoothDevice? device;
        if (File.Exists(deviceIdFile))
        {
            // Reuse device
            string deviceId = File.ReadAllText(deviceIdFile);
            device = await BluetoothDevice.FromIdAsync(deviceId);

            if (device != null)
            {
                if (!device.Gatt.IsConnected)  await device.Gatt.ConnectAsync();
                return device;
            }
        }

        if (!showDialog)
        {
            return null;
        }


        // Scan for new device
        device = await Bluetooth.RequestDeviceAsync(new RequestDeviceOptions
        {
            AcceptAllDevices = true,
            OptionalServices = { optionalService } // Service UUID
        });

        if (device != null)
        {
            File.WriteAllText(deviceIdFile, device.Id);
            if (!device.Gatt.IsConnected) await device.Gatt.ConnectAsync();
       
            return device;
        }

        return null;
    }

    public static string GetAppFolder()
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            string documentsPath = Path.Combine("/storage/emulated/0/Documents", "Re-Run");
            if (!Directory.Exists(documentsPath))
                Directory.CreateDirectory(documentsPath);

            return documentsPath;
        }
        else
        {
            // Default to the app-specific folder for other platforms  
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Re-Run");
        }
    }       


    public static decimal GetSpeed(decimal incline)
    {
        if (incline < 0)
            return (decimal)RunSettings.Speed0to5 + 0.5m;
        if (incline <= 5)
            return (decimal)RunSettings.Speed0to5;
        if (incline <= 8)
            return (decimal)RunSettings.Speed6to8;
        if (incline <= 10)
            return (decimal)RunSettings.Speed8to10;
        if (incline <= 12)
            return (decimal)RunSettings.Speed11to12;
        if (incline <= 15)
            return (decimal)RunSettings.Speed13to15;

        return (decimal)RunSettings.Speed13to15 - 0.3m;
    }

}

