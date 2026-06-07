using Microsoft.Maui.Storage;

namespace BKPos.Mobile.App.Services;

public sealed class MobileLoginSettings
{
    private const string UsernameKey = "login_username";
    private const string RememberKey = "login_remember";
    private const string AutoLoginKey = "login_auto";
    private const string PasswordKey = "login_password";

    public async Task<SavedLogin> LoadAsync()
    {
        var remember = Preferences.Default.Get(RememberKey, false);
        var autoLogin = Preferences.Default.Get(AutoLoginKey, false);
        var username = remember ? Preferences.Default.Get(UsernameKey, string.Empty) : string.Empty;
        var password = string.Empty;

        if (remember)
        {
            try
            {
                password = await SecureStorage.Default.GetAsync(PasswordKey) ?? string.Empty;
            }
            catch
            {
                password = string.Empty;
            }
        }

        return new SavedLogin(username, password, remember, remember && autoLogin);
    }

    public async Task SaveAsync(string username, string password, bool remember, bool autoLogin)
    {
        Preferences.Default.Set(RememberKey, remember);
        Preferences.Default.Set(AutoLoginKey, remember && autoLogin);

        if (!remember)
        {
            Preferences.Default.Remove(UsernameKey);
            SecureStorage.Default.Remove(PasswordKey);
            return;
        }

        Preferences.Default.Set(UsernameKey, username.Trim());
        await SecureStorage.Default.SetAsync(PasswordKey, password);
    }

    public void DisableAutoLogin()
    {
        Preferences.Default.Set(AutoLoginKey, false);
    }
}

public sealed record SavedLogin(string Username, string Password, bool Remember, bool AutoLogin);
