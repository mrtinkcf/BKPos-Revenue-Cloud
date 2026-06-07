using Android.Provider;
using BKPos.Core.Interfaces;
using BKPos.Licensing;

namespace BKPos.Mobile.App.Platforms.Android;

public sealed class AndroidHardwareIdProvider : IHardwareIdProvider
{
    public string GetHardwareId()
    {
        var context = global::Android.App.Application.Context;
        var androidId = Settings.Secure.GetString(context.ContentResolver, Settings.Secure.AndroidId);
        if (string.IsNullOrWhiteSpace(androidId))
        {
            throw new InvalidOperationException("Android ID is not available on this device.");
        }

        return HardwareIdFormatter.CreateFromRawId(androidId);
    }
}
