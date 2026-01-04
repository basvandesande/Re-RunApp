namespace Re_RunApp;

using Re_RunApp.Core;
using Re_RunApp.Views;
using System.Reflection;
using System.Threading.Tasks;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Run initialization (restore access + prepare files) asynchronously.
        // InitializeAsync will set MainPage once done.
        _ = InitializeAsync();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Create the main window and set its page to AppShell.
        var window = new Window(new AppShell());
        return window;
    }


    private async Task InitializeAsync()
    {
        // Try to restore access using persisted FutureAccessList token (if present).
        // This ensures SetUserAppFolderFromToken can recreate the persisted path before PrepareFolderAndDefaultFile runs.
        try
        {
            await Runtime.TryRestoreFolderAccessFromTokenAsync();
        }
        catch
        {
            // non-fatal, continue with defaults
        }

        // Ensure default files are copied to data folder on first run
        PrepareFolderAndDefaultFile();

        // Now show UI
        // Instead of MainPage = new AppShell();
        // Use the recommended approach to set the root page:
        if (Windows.Count > 0)
        {
            Windows[0].Page = new AppShell();
        }
    }

    private void PrepareFolderAndDefaultFile()
    {
        try
        {
            string folder = Runtime.GetAppFolder();
            System.Diagnostics.Debug.WriteLine($"Preparing folder: {folder}");

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                System.Diagnostics.Debug.WriteLine($"Created folder: {folder}");
            }

            string gpxFilePath = Path.Combine(folder, "Climbing from Prato to Gomagoi.gpx");
            string resourceName = "Re_RunApp.Resources.Files.prato-gomagoi.gpx";
            CopyEmbeddedResourceToFile(resourceName, gpxFilePath);

            string videoFilePath = Path.Combine(folder, "Climbing from Prato to Gomagoi.mp4");
            string videoName = "Re_RunApp.Resources.Raw.prato-gomagoi.mp4";
            CopyEmbeddedResourceToFile(videoName, videoFilePath);

            System.Diagnostics.Debug.WriteLine("Default files preparation completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error preparing default files: {ex.Message}");
        }
    }

    private void CopyEmbeddedResourceToFile(string resourceName, string destinationPath)
    {
        try
        {
            if (!File.Exists(destinationPath))
            {
                var assembly = Assembly.GetExecutingAssembly();
                System.Diagnostics.Debug.WriteLine($"Attempting to copy resource '{resourceName}' to '{destinationPath}'");

                using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                        {
                            stream.CopyTo(fileStream);
                        }
                        System.Diagnostics.Debug.WriteLine($"Successfully copied '{resourceName}' to '{destinationPath}'");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Embedded resource '{resourceName}' not found in assembly");
                        
                        // Debug: List all available resources
                        var availableResources = assembly.GetManifestResourceNames();
                        System.Diagnostics.Debug.WriteLine($"Available embedded resources: {string.Join(", ", availableResources)}");
                    }
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"File already exists, skipping: {destinationPath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error copying embedded resource '{resourceName}': {ex.Message}");
        }
    }

}
