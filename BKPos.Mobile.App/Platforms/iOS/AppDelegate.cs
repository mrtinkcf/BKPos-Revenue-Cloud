using Foundation;
using UIKit;

namespace BKPos.Mobile.App;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    public static UIInterfaceOrientationMask AllowedOrientations { get; set; } =
        UIInterfaceOrientationMask.Landscape;

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override UIInterfaceOrientationMask GetSupportedInterfaceOrientations(
        UIApplication application, UIWindow? forWindow) => AllowedOrientations;
}
