namespace Re_RunApp;

using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;
using OxyPlot.Maui.Skia;
using Microsoft.Extensions.DependencyInjection;
using Re_RunApp.Core;
#if WINDOWS
using Re_RunApp.Platforms.Windows;
#endif

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement() // Fix for MCTME001  
            .UseSkiaSharp()
            .UseOxyPlotSkia()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("NotoSansCJKsc-Regular.otf", "NotoSansCJKsc");
                fonts.AddFont("PermanentMarker-Regular.ttf", "PermanentMarker");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Register platform folder picker implementation on Windows
#if WINDOWS
        builder.Services.AddSingleton<IFolderPicker, FolderPickerImplementation>();
#endif

        return builder.Build();
    }

}
