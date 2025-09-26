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

        if (!File.Exists(gpxFilePath))
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = "Re_RunApp.Resources.Files.prato-gomagoi.gpx";

            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using (var fileStream = new FileStream(gpxFilePath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
            }
        }
    }


}
