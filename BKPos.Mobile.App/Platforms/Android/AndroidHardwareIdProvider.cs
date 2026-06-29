using Android.Provider;
using BKPos.Core.Interfaces;
using BKPos.Licensing;
using Microsoft.Maui.Storage;

namespace BKPos.Mobile.App.Platforms.Android;

public sealed class AndroidHardwareIdProvider : IHardwareIdProvider
{
    private const string StableHardwareIdKey = "android_hardware_id";
    private const string ActivatedHardwareIdKey = "mobile_license_hardware_id";

    public string GetHardwareId()
    {
        var stableHardwareId = Preferences.Default.Get(StableHardwareIdKey, string.Empty);
        if (LicenseFormat.TryNormalizeHardwareId(stableHardwareId, out var normalizedStableHardwareId))
        {
            return normalizedStableHardwareId;
        }

        // Keep the hardware id that was already activated by older app builds.
        var activatedHardwareId = Preferences.Default.Get(ActivatedHardwareIdKey, string.Empty);
        if (LicenseFormat.TryNormalizeHardwareId(activatedHardwareId, out var normalizedActivatedHardwareId))
        {
            Preferences.Default.Set(StableHardwareIdKey, normalizedActivatedHardwareId);
            return normalizedActivatedHardwareId;
        }

        var context = global::Android.App.Application.Context;
        var androidId = Settings.Secure.GetString(context.ContentResolver, Settings.Secure.AndroidId);
        if (string.IsNullOrWhiteSpace(androidId))
        {
            throw new InvalidOperationException("Android ID is not available on this device.");
        }

        return SaveStableHardwareId(HardwareIdFormatter.CreateFromRawId(androidId));
    }

    private static string SaveStableHardwareId(string hardwareId)
    {
        var normalizedHardwareId = LicenseFormat.TryNormalizeHardwareId(hardwareId, out var normalized)
            ? normalized
            : hardwareId.Trim();
        Preferences.Default.Set(StableHardwareIdKey, normalizedHardwareId);
        return normalizedHardwareId;
    }
}
