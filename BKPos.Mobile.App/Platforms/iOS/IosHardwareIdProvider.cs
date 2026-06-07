using BKPos.Core.Interfaces;
using BKPos.Licensing;
using UIKit;

namespace BKPos.Mobile.App.Platforms.iOS;

public sealed class IosHardwareIdProvider : IHardwareIdProvider
{
    private const string FallbackIdKey = "ios_hardware_fallback_id";

    public string GetHardwareId()
    {
        var vendorId = UIDevice.CurrentDevice.IdentifierForVendor?.AsString();
        var rawId = string.IsNullOrWhiteSpace(vendorId) ? GetOrCreateFallbackId() : vendorId.Trim();

        return HardwareIdFormatter.CreateFromRawId("ios:" + rawId);
    }

    private static string GetOrCreateFallbackId()
    {
        var stored = Preferences.Default.Get(FallbackIdKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            return stored.Trim();
        }

        var generated = Guid.NewGuid().ToString("N");
        Preferences.Default.Set(FallbackIdKey, generated);
        return generated;
    }
}
