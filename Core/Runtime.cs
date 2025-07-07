namespace Re_RunApp.Core;

using InTheHand.Bluetooth;

internal class Runtime
{
    internal static Treadmill Treadmill { get; set; } = new Treadmill();
    internal static TreadmillSimulator TreadmillSimulator { get; set; } = new TreadmillSimulator();
    internal static HeartRate HeartRate { get; set; } = new HeartRate();
    internal static HeartRateSimulator HeartRateSimulator { get; set; } = new HeartRateSimulator();

    internal static RunSettings? RunSettings { get; set; }

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
}

