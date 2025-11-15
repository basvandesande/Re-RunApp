using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;

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

    public static bool IsUserFolderPersisted()
    {
        // Return true when a user-selected folder path was persisted and the folder still exists.
        try
        {
            var persistFile = GetPersistedUserFolderFilePath();
            if (!File.Exists(persistFile))
                return false;

            var userFolder = File.ReadAllText(persistFile).Trim();
            if (string.IsNullOrWhiteSpace(userFolder))
                return false;

            return Directory.Exists(userFolder);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"IsUserFolderPersisted failed: {ex.Message}");
            return false;
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

    // Persisted user folder file path inside the MAUI AppDataDirectory
    private static string GetPersistedUserFolderFilePath()
    {
        var local = Path.Combine(FileSystem.AppDataDirectory, "Re-Run");
        if (!Directory.Exists(local))
            Directory.CreateDirectory(local);
        return Path.Combine(local, "user_folder.txt");
    }

    // Set the user-selected folder and create the "re-run" subfolder
    public static void SetUserAppFolder(string selectedFolderPath)
    {
        if (string.IsNullOrWhiteSpace(selectedFolderPath))
            return;

        try
        {
            // Persist the user folder path (outside of the chosen folder)
            var persistFile = GetPersistedUserFolderFilePath();
            File.WriteAllText(persistFile, selectedFolderPath);

            // Ensure the chosen folder exists and create the subfolder "re-run"
            var reRunFolder = Path.Combine(selectedFolderPath, "re-run");
            if (!Directory.Exists(reRunFolder))
                Directory.CreateDirectory(reRunFolder);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set user app folder: {ex.Message}");
        }
    }

    // Get the app folder — prefer previously chosen folder (and ensure "re-run" exists)
    public static string GetAppFolder()
    {
        // Check persisted user folder
        try
        {
            var persistFile = GetPersistedUserFolderFilePath();
            if (File.Exists(persistFile))
            {
                var userFolder = File.ReadAllText(persistFile).Trim();
                if (!string.IsNullOrWhiteSpace(userFolder) && Directory.Exists(userFolder))
                {
                    var reRunFolder = Path.Combine(userFolder, "re-run");
                    if (!Directory.Exists(reRunFolder))
                    {
                        try { Directory.CreateDirectory(reRunFolder); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Could not create re-run folder in user folder: {ex.Message}"); }
                    }
                    return reRunFolder;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading persisted user folder: {ex.Message}");
        }

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
        else // Default for Windows and other platforms, keep previous LocalApplicationData approach
        {
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

    private static string GetPersistedUserFolderTokenFilePath()
    {
        var local = Path.Combine(FileSystem.AppDataDirectory, "Re-Run");
        if (!Directory.Exists(local))
            Directory.CreateDirectory(local);
        return Path.Combine(local, "user_folder_token.txt");
    }

    // Persist path + optional FutureAccessList token
    public static void SetUserAppFolderWithToken(string selectedFolderPath, string? futureAccessToken)
    {
        if (string.IsNullOrWhiteSpace(selectedFolderPath))
            return;

        try
        {
            var persistFile = GetPersistedUserFolderFilePath();
            File.WriteAllText(persistFile, selectedFolderPath);

            if (!string.IsNullOrWhiteSpace(futureAccessToken))
                File.WriteAllText(GetPersistedUserFolderTokenFilePath(), futureAccessToken);

            var reRunFolder = Path.Combine(selectedFolderPath, "re-run");
            if (!Directory.Exists(reRunFolder))
                Directory.CreateDirectory(reRunFolder);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to set user app folder with token: {ex.Message}");
        }
    }

    public static string? GetPersistedUserFolderToken()
    {
        var tokenFile = GetPersistedUserFolderTokenFilePath();
        if (File.Exists(tokenFile))
            return File.ReadAllText(tokenFile).Trim();
        return null;
    }

    // Replace the conditional-method with a single always-available method.
    public static async Task TryRestoreFolderAccessFromTokenAsync()
    {
        try
        {
#if WINDOWS
            var token = GetPersistedUserFolderToken();
            if (string.IsNullOrWhiteSpace(token))
                return;

            var folder = await Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
            if (folder is not null)
            {
                var path = folder.Path;
                var persistFile = GetPersistedUserFolderFilePath();
               
                var reRunFolder = Path.Combine(path, "Re-Run");
                if (!Directory.Exists(reRunFolder))
                    Directory.CreateDirectory(reRunFolder);

                File.WriteAllText(persistFile, path);
            }
#else
            // no-op on non-Windows platforms
            await Task.CompletedTask;
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Restore from token failed: {ex.Message}");
        }
    }
}

