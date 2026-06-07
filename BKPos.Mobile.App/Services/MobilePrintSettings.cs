using Microsoft.Maui.Storage;

namespace BKPos.Mobile.App.Services;

public sealed class MobilePrintSettings
{
    private const string AutoKitchenEnabledKey = "print_auto_kitchen_enabled";
    private const string AutoKitchenDelayMinutesKey = "print_auto_kitchen_delay_minutes";

    public bool AutoKitchenPrintEnabled
    {
        get => Preferences.Default.Get(AutoKitchenEnabledKey, false);
        set => Preferences.Default.Set(AutoKitchenEnabledKey, value);
    }

    public int AutoKitchenPrintDelayMinutes
    {
        get => Math.Clamp(Preferences.Default.Get(AutoKitchenDelayMinutesKey, 2), 1, 120);
        set => Preferences.Default.Set(AutoKitchenDelayMinutesKey, Math.Clamp(value, 1, 120));
    }

    public void Save(bool enabled, int delayMinutes)
    {
        AutoKitchenPrintEnabled = enabled;
        AutoKitchenPrintDelayMinutes = delayMinutes;
    }
}
