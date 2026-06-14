using Microsoft.Maui.Storage;

namespace BKPos.Mobile.App.Services;

public sealed class MobileOrientationSettings
{
    public const string Landscape = "landscape";
    public const string Portrait = "portrait";

    private const string ModeKey = "orientation_mode";
    private const string RememberKey = "orientation_remember";

    public (string? mode, bool remember) Load()
    {
        var remember = Preferences.Default.Get(RememberKey, false);
        if (!remember)
            return (null, false);
        var mode = Preferences.Default.Get(ModeKey, string.Empty);
        return (string.IsNullOrEmpty(mode) ? null : mode, true);
    }

    public void Save(string mode, bool remember)
    {
        Preferences.Default.Set(RememberKey, remember);
        if (remember)
            Preferences.Default.Set(ModeKey, mode);
        else
            Preferences.Default.Remove(ModeKey);
    }
}
