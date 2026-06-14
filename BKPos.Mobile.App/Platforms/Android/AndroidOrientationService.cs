using Android.Content.PM;
using BKPos.Mobile.App.Services;

namespace BKPos.Mobile.App.Platforms.Android;

public sealed class AndroidOrientationService : IOrientationService
{
    public void LockLandscape()
    {
        if (MainActivity.Current is { } activity)
            activity.RequestedOrientation = ScreenOrientation.SensorLandscape;
    }

    public void LockPortrait()
    {
        if (MainActivity.Current is { } activity)
            activity.RequestedOrientation = ScreenOrientation.SensorPortrait;
    }
}
