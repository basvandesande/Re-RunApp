using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;

namespace Re_RunApp;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement() // Fix for MCTME001  
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }

    public static string GetAppFolder() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Re-Run");
}
