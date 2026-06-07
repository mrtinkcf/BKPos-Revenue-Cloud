using Microsoft.Maui.Storage;

namespace BKPos.Mobile.App.Services;

internal static class MobileActivationStore
{
    private const string HardwareIdKey = "mobile_license_hardware_id";
    private const string LicenseIdKey = "mobile_license_id";

    public static bool IsActivatedFor(string hardwareId, string? licenseId)
    {
        var storedHardwareId = Preferences.Default.Get(HardwareIdKey, string.Empty);
        if (string.IsNullOrWhiteSpace(storedHardwareId)
            || !string.Equals(storedHardwareId.Trim(), hardwareId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var storedLicenseId = Preferences.Default.Get(LicenseIdKey, string.Empty);
        return string.IsNullOrWhiteSpace(licenseId)
            || string.IsNullOrWhiteSpace(storedLicenseId)
            || string.Equals(storedLicenseId.Trim(), licenseId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public static void MarkActivated(string hardwareId, string? licenseId)
    {
        Preferences.Default.Set(HardwareIdKey, hardwareId.Trim());
        if (!string.IsNullOrWhiteSpace(licenseId))
        {
            Preferences.Default.Set(LicenseIdKey, licenseId.Trim());
        }
    }
}
