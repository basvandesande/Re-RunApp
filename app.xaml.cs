namespace Re_RunApp;

using Re_RunApp.Core;
using Re_RunApp.Views;
using System.Reflection;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        PrepareFolderAndDefaultFile();    
        MainPage = new AppShell();
    }



    private void PrepareFolderAndDefaultFile()
    {
        string folder = Runtime.GetAppFolder();

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string gpxFilePath = Path.Combine(folder, "Climbing from Prato to Gomagoi.gpx");
        string resourceName = "Re_RunApp.Resources.Files.prato-gomagoi.gpx";
        CopyEmbeddedResourceToFile(resourceName, gpxFilePath);

        string videoFilePath = Path.Combine(folder, "Climbing from Prato to Gomagoi.mp4");
        string videoName = "Re_RunApp.Resources.Raw.prato-gomagoi.mp4";
        CopyEmbeddedResourceToFile(videoName, videoFilePath);
    }


    private void CopyEmbeddedResourceToFile(string resourceName, string destinationPath)
    {
        if (!File.Exists(destinationPath))
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
            }
        }
    }

}
