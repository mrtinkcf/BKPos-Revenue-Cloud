using System.Text.Json;
using Microsoft.Maui.Storage;

namespace BKPos.Revenue.App;

public sealed class RevenueOfflineCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxRangeEntries = 10;
    private const int MaxInvoiceDetails = 100;

    public async Task SaveAsync<T>(string tenantId, string storeId, string userId, string key, T value)
    {
        var envelope = new CacheEnvelope<T>(DateTimeOffset.Now, value);
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        var cacheKey = CacheKey(tenantId, storeId, userId, key);
        await SecureStorage.Default.SetAsync(cacheKey, json);
        TrackKey(tenantId, storeId, userId, cacheKey);
        if (key.StartsWith("range_", StringComparison.OrdinalIgnoreCase))
        {
            TrimLimitedIndex(tenantId, storeId, userId, "range", key, MaxRangeEntries);
        }
    }

    public async Task<CacheEnvelope<T>?> LoadAsync<T>(string tenantId, string storeId, string userId, string key)
    {
        var json = await SecureStorage.Default.GetAsync(CacheKey(tenantId, storeId, userId, key));
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CacheEnvelope<T>>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveInvoiceDetailAsync(string tenantId, string storeId, string userId, string invoiceId, InvoiceDetailResponse value)
    {
        var key = InvoiceDetailKey(invoiceId);
        await SaveAsync(tenantId, storeId, userId, key, value);
        TrimLimitedIndex(tenantId, storeId, userId, "invoice_details", key, MaxInvoiceDetails);
    }

    public Task<CacheEnvelope<InvoiceDetailResponse>?> LoadInvoiceDetailAsync(string tenantId, string storeId, string userId, string invoiceId)
        => LoadAsync<InvoiceDetailResponse>(tenantId, storeId, userId, InvoiceDetailKey(invoiceId));

    public void ClearUserCache(string tenantId, string storeId, string userId)
    {
        foreach (var key in ReadIndex(TrackedIndexKey(tenantId, storeId, userId)))
        {
            SecureStorage.Default.Remove(key);
        }
        Preferences.Default.Remove(TrackedIndexKey(tenantId, storeId, userId));
        Preferences.Default.Remove(LimitedIndexKey(tenantId, storeId, userId, "range"));
        Preferences.Default.Remove(LimitedIndexKey(tenantId, storeId, userId, "invoice_details"));
    }

    private static string InvoiceDetailKey(string invoiceId)
        => $"invoice_detail_{invoiceId.Trim()}";

    private static string CacheKey(string tenantId, string storeId, string userId, string key)
        => $"revenue_cache_{Safe(tenantId)}_{Safe(storeId)}_{Safe(userId)}_{Safe(key)}";

    private static string TrackedIndexKey(string tenantId, string storeId, string userId)
        => $"revenue_cache_index_{Safe(tenantId)}_{Safe(storeId)}_{Safe(userId)}";

    private static string LimitedIndexKey(string tenantId, string storeId, string userId, string kind)
        => $"revenue_cache_lru_{Safe(tenantId)}_{Safe(storeId)}_{Safe(userId)}_{Safe(kind)}";

    private static void TrackKey(string tenantId, string storeId, string userId, string cacheKey)
    {
        var indexKey = TrackedIndexKey(tenantId, storeId, userId);
        var keys = ReadIndex(indexKey);
        if (!keys.Contains(cacheKey, StringComparer.Ordinal))
        {
            keys.Add(cacheKey);
            WriteIndex(indexKey, keys);
        }
    }

    private static void TrimLimitedIndex(string tenantId, string storeId, string userId, string kind, string itemKey, int max)
    {
        var indexKey = LimitedIndexKey(tenantId, storeId, userId, kind);
        var keys = ReadIndex(indexKey);
        keys.RemoveAll(x => string.Equals(x, itemKey, StringComparison.Ordinal));
        keys.Insert(0, itemKey);
        foreach (var stale in keys.Skip(max).ToList())
        {
            SecureStorage.Default.Remove(CacheKey(tenantId, storeId, userId, stale));
        }
        WriteIndex(indexKey, keys.Take(max).ToList());
    }

    private static List<string> ReadIndex(string key)
    {
        var raw = Preferences.Default.Get(key, string.Empty);
        return string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList();
    }

    private static void WriteIndex(string key, IEnumerable<string> values)
        => Preferences.Default.Set(key, string.Join("|", values));

    private static string Safe(string value)
        => Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(value.Trim()));
}

public sealed record CacheEnvelope<T>(DateTimeOffset CachedAt, T Value)
{
    public bool IsStale => DateTimeOffset.Now - CachedAt > TimeSpan.FromDays(7);
}
