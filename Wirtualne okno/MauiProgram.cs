using Microsoft.Extensions.Logging;
using Wirtualne_okno.Platforms.Android;

namespace Wirtualne_okno;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        builder.Services.AddSingleton<IHeadTrackingService, AndroidHeadTrackingService>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<MainPage2>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}