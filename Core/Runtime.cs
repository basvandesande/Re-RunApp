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

    public static bool OpenFolderInExplorer(string folderPath)
    {
        try
        {
            // Ensure the directory exists before trying to open it
            if (!Directory.Exists(folderPath))
            {
                System.Diagnostics.Debug.WriteLine($"Directory does not exist, creating: {folderPath}");
                Directory.CreateDirectory(folderPath);
                
                // Verify the directory was actually created
                if (!Directory.Exists(folderPath))
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create directory: {folderPath}");
                    return false;
                }
                
                // Add a small delay to ensure the filesystem has updated
                System.Threading.Thread.Sleep(100);
            }

            // Double-check the directory exists before opening
            if (!Directory.Exists(folderPath))
            {
                System.Diagnostics.Debug.WriteLine($"Directory still does not exist after creation attempt: {folderPath}");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"Opening folder in explorer: {folderPath}");

            if (DeviceInfo.Platform == DevicePlatform.WinUI || OperatingSystem.IsWindows())
            {
#if WINDOWS
                // Use the proper Windows explorer command with quotes around the path
                // This ensures the exact folder is opened, not the default Documents folder
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folderPath}\"",  // Wrap path in quotes
                    UseShellExecute = true,
                    CreateNoWindow = true
                };
                
                var process = System.Diagnostics.Process.Start(processStartInfo);
                
                // Verify the process started successfully
                if (process != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Explorer process started successfully for: {folderPath}");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to start explorer process for: {folderPath}");
                    return false;
                }
#endif
            }
            else if (DeviceInfo.Platform == DevicePlatform.MacCatalyst)
            {
                // macOS - also ensure directory exists
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true
                };
                
                var process = System.Diagnostics.Process.Start(processStartInfo);
                return process != null;
            }
            else if (DeviceInfo.Platform == DevicePlatform.Android)
            {
                // Android - this is more complex and may require specific intents
                // For now, we'll return false as it's not straightforward
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening folder: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        return false;
    }

    public static async Task<BluetoothDevice?> GetOrRequestDeviceAsync(string deviceIdFile, Guid optionalService, bool showDialog = true)
    {
        BluetoothDevice? device;
        if (File.Exists(deviceIdFile))
        {
            // Reuse device
            string deviceId = File.ReadAllText(deviceIdFile);
            device = await BluetoothDevice.FromIdAsync(deviceId);

            if (device != null)
            {
                if (!device.Gatt.IsConnected) await device.Gatt.ConnectAsync();
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
        // Debug: Print platform information
        System.Diagnostics.Debug.WriteLine($"Current Platform: {DeviceInfo.Platform}");
        System.Diagnostics.Debug.WriteLine($"Platform String: {DeviceInfo.Platform.ToString()}");
        
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            string documentsPath = Path.Combine("/storage/emulated/0/Documents", "Re-Run");
            if (!Directory.Exists(documentsPath))
                Directory.CreateDirectory(documentsPath);

            System.Diagnostics.Debug.WriteLine($"Using Android path: {documentsPath}");
            return documentsPath;
        }
        else //if (OperatingSystem.IsWindows()) // Additional check for Windows
        {
            // Use LocalApplicationData for Windows to avoid permission issues
            string localAppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Re-Run");

            if (!Directory.Exists(localAppData))
            {
                try
                {
                    Directory.CreateDirectory(localAppData);
                    System.Diagnostics.Debug.WriteLine($"Created Windows LocalApplicationData directory: {localAppData}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to create Windows LocalApplicationData directory: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"Using Windows LocalApplicationData path: {localAppData}");
            return localAppData;
        }
        // ----------------------------------------------------------------------------------------------------
        // attention: the above 'else' now includes all non-Android platforms, including iOS and MacCatalyst.
        // ----------------------------------------------------------------------------------------------------
        //else
        //{
        //    // Default to the app-specific folder for other platforms  
        //    string personalPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Re-Run");

        //    if (!Directory.Exists(personalPath))
        //        Directory.CreateDirectory(personalPath);

        //    System.Diagnostics.Debug.WriteLine($"Using Personal folder path: {personalPath}");
        //    return personalPath;
        //}
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

