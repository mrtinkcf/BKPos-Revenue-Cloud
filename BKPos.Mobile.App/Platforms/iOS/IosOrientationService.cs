using UIKit;
using Foundation;
using BKPos.Mobile.App.Services;

namespace BKPos.Mobile.App.Platforms.iOS;

public sealed class IosOrientationService : IOrientationService
{
    public void LockLandscape()
    {
        AppDelegate.AllowedOrientations = UIInterfaceOrientationMask.Landscape;
        Rotate(UIInterfaceOrientation.LandscapeLeft);
    }

    public void LockPortrait()
    {
        AppDelegate.AllowedOrientations = UIInterfaceOrientationMask.Portrait;
        Rotate(UIInterfaceOrientation.Portrait);
    }

    private static void Rotate(UIInterfaceOrientation orientation)
    {
        UIDevice.CurrentDevice.SetValueForKey(
            new NSNumber((int)orientation),
            new NSString("orientation"));
    }
}
