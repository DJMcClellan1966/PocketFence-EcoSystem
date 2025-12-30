using Microsoft.Extensions.Logging;
using PocketFence.Library;

namespace PocketFenceApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        // Register PocketFence services
        builder.Services.AddSingleton<PocketFenceEngine>();
        builder.Services.AddSingleton<MainPageViewModel>();
        builder.Services.AddTransient<MainPage>();

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddLogging();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}