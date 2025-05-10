namespace Re_RunApp;

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
        string folder = MauiProgram.GetAppFolder();

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string gpxFilePath = Path.Combine(folder, "Stelvio - part I.gpx");

        if (!File.Exists(gpxFilePath))
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = "Re_RunApp.Resources.Files.Stelvio.gpx";

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
