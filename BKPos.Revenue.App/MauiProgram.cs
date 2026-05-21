using Microsoft.Extensions.Logging;

namespace BKPos.Revenue.App;

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
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        {
            Timeout = TimeSpan.FromSeconds(20)
        });
        builder.Services.AddSingleton<RevenueSessionStore>();
        builder.Services.AddSingleton<RevenueOfflineCache>();
        builder.Services.AddSingleton<RevenueApiClient>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<DashboardPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
