using BKPos.Core.Interfaces;
using BKPos.Licensing;
using UIKit;

namespace BKPos.Mobile.App.Platforms.iOS;

public sealed class IosHardwareIdProvider : IHardwareIdProvider
{
    private const string StableHardwareIdKey = "ios_hardware_id";
    private const string FallbackIdKey = "ios_hardware_fallback_id";
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

        var legacyFallbackRawId = Preferences.Default.Get(FallbackIdKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(legacyFallbackRawId))
        {
            return SaveStableHardwareId(HardwareIdFormatter.CreateFromRawId("ios:" + legacyFallbackRawId.Trim()));
        }

        var vendorId = UIDevice.CurrentDevice.IdentifierForVendor?.AsString();
        var rawId = string.IsNullOrWhiteSpace(vendorId) ? CreateFallbackId() : vendorId.Trim();

        return SaveStableHardwareId(HardwareIdFormatter.CreateFromRawId("ios:" + rawId));
    }

    private static string CreateFallbackId()
    {
        var generated = Guid.NewGuid().ToString("N");
        Preferences.Default.Set(FallbackIdKey, generated);
        return generated;
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
