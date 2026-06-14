using BKPos.Core.Formatting;
using BKPos.Core.Interfaces;
using BKPos.Mobile.App.Pages;
using BKPos.Mobile.App.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using ZXing.Net.Maui.Controls;

#if ANDROID
using BKPos.Mobile.App.Platforms.Android;
using Android.Content;
using Android.Views.InputMethods;
#elif IOS
using BKPos.Mobile.App.Platforms.iOS;
using UIKit;
#endif



namespace BKPos.Mobile.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MoneyFormatter.ApplyDefaultCulture();

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseBarcodeReader()
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                EntryHandler.Mapper.AppendToMapping("BKPosNoFullscreenIme", (handler, _) =>
                {
                    DisableNativeKeyboard(handler);
                });
#elif IOS
                EntryHandler.Mapper.AppendToMapping("BKPosPlainEntry", (handler, _) =>
                {
                    handler.PlatformView.BorderStyle = UITextBorderStyle.None;
                    handler.PlatformView.BackgroundColor = UIColor.Clear;
                });
#endif
            })
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton(_ => new HttpClient(new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(8),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        {
            Timeout = TimeSpan.FromSeconds(15)
        });
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<MobileLoginSettings>();
        builder.Services.AddSingleton<MobilePrintSettings>();
#if ANDROID
        builder.Services.AddSingleton<INetworkDiscovery, AndroidNetworkDiscovery>();
        builder.Services.AddSingleton<IHardwareIdProvider, AndroidHardwareIdProvider>();
#elif IOS
        builder.Services.AddSingleton<INetworkDiscovery, IosNetworkDiscovery>();
        builder.Services.AddSingleton<IHardwareIdProvider, IosHardwareIdProvider>();
#endif
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<ServerSetupPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

#if ANDROID
        OrientationService.Initialize(new AndroidOrientationService());
#elif IOS
        OrientationService.Initialize(new IosOrientationService());
#endif

        return builder.Build();
    }

#if ANDROID
    private static void DisableNativeKeyboard(IEntryHandler handler)
    {
        var editText = handler.PlatformView;
        editText.SetSingleLine(true);
        editText.ShowSoftInputOnFocus = false;
        editText.ImeOptions = (ImeAction)((int)editText.ImeOptions
            | (int)ImeFlags.NoExtractUi
            | (int)ImeFlags.NoFullscreen);

        void Hide()
        {
            editText.ShowSoftInputOnFocus = false;
            var inputMethodManager = (InputMethodManager?)editText.Context.GetSystemService(Context.InputMethodService);
            inputMethodManager?.HideSoftInputFromWindow(editText.WindowToken, HideSoftInputFlags.None);
        }

        editText.FocusChange += (_, _) => Hide();
        editText.Click += (_, _) => Hide();
        editText.TextChanged += (_, _) => Hide();
    }
#endif
}




