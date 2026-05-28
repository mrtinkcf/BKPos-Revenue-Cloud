using Microsoft.Maui.Storage;

namespace BKPos.Revenue.App;

public sealed class RevenueSessionStore
{
    private const string WorkerUrlKey = "revenue_worker_url";
    private const string TenantIdKey = "revenue_tenant_id";
    private const string StoreIdKey = "revenue_store_id";
    private const string UsernameKey = "revenue_username";
    private const string RememberCredentialsKey = "revenue_remember_credentials";
    private const string SavedPasswordKey = "revenue_saved_password";
    private const string AccessTokenKey = "revenue_access_token";
    private const string RefreshTokenKey = "revenue_refresh_token";

    public string WorkerUrl
    {
        get => Preferences.Default.Get(WorkerUrlKey, string.Empty);
        set => Preferences.Default.Set(WorkerUrlKey, NormalizeUrl(value));
    }

    public string TenantId
    {
        get => Preferences.Default.Get(TenantIdKey, string.Empty);
        set => Preferences.Default.Set(TenantIdKey, value.Trim());
    }

    public string StoreId
    {
        get => Preferences.Default.Get(StoreIdKey, string.Empty);
        set => Preferences.Default.Set(StoreIdKey, value.Trim());
    }

    public string Username
    {
        get => Preferences.Default.Get(UsernameKey, string.Empty);
        set => Preferences.Default.Set(UsernameKey, value.Trim());
    }

    public bool RememberCredentials
    {
        get => Preferences.Default.Get(RememberCredentialsKey, false);
        set => Preferences.Default.Set(RememberCredentialsKey, value);
    }

    public async Task SaveTokensAsync(string accessToken, string refreshToken)
    {
        await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
    }

    public async Task<string> GetAccessTokenAsync()
        => await SecureStorage.Default.GetAsync(AccessTokenKey) ?? string.Empty;

    public async Task<string> GetRefreshTokenAsync()
        => await SecureStorage.Default.GetAsync(RefreshTokenKey) ?? string.Empty;

    public async Task SavePasswordAsync(string password)
        => await SecureStorage.Default.SetAsync(SavedPasswordKey, password);

    public async Task<string> GetSavedPasswordAsync()
        => await SecureStorage.Default.GetAsync(SavedPasswordKey) ?? string.Empty;

    public void ClearSavedPassword()
    {
        SecureStorage.Default.Remove(SavedPasswordKey);
    }

    public async Task<bool> HasSessionAsync()
        => !string.IsNullOrWhiteSpace(await GetRefreshTokenAsync());

    public void ClearTokens()
    {
        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
    }

    public void ClearAll()
    {
        ClearTokens();
        Preferences.Default.Remove(StoreIdKey);
        Preferences.Default.Remove(UsernameKey);
        Preferences.Default.Remove(RememberCredentialsKey);
        ClearSavedPassword();
    }

    private static string NormalizeUrl(string value)
        => value.Trim().TrimEnd('/');
}
