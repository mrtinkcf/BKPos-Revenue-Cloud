namespace BKPos.Mobile.App;

using BKPos.Core.Interfaces;
using BKPos.Mobile.App.Pages;
using BKPos.Mobile.App.Services;

public partial class AppShell : Shell
{
    public AppShell(
        ApiClient api,
        IHardwareIdProvider hardwareIdProvider,
        MobileLoginSettings loginSettings,
        MobilePrintSettings printSettings)
    {
        InitializeComponent();
        FlyoutBehavior = FlyoutBehavior.Disabled;
        Items.Add(new ShellContent
        {
            Title = "Đăng nhập",
            Route = "LoginPage",
            Content = new LoginPage(api, hardwareIdProvider, loginSettings, printSettings)
        });
    }
}
