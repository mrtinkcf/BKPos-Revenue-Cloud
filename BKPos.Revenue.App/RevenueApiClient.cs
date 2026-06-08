using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BKPos.Revenue.App;

public sealed class RevenueApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly RevenueSessionStore _session;
    private readonly RevenueOfflineCache _cache;

    public RevenueCacheStatus CacheStatus { get; private set; } = RevenueCacheStatus.Online;

    public RevenueApiClient(HttpClient http, RevenueSessionStore session, RevenueOfflineCache cache)
    {
        _http = http;
        _session = session;
        _cache = cache;
    }

    public async Task<LoginResponse> LoginAsync(string workerUrl, string tenantId, string username, string password, CancellationToken cancellationToken = default)
    {
        _session.WorkerUrl = workerUrl.Trim().TrimEnd('/');
        _session.TenantId = tenantId.Trim();
        _session.Username = username.Trim();
        var response = await PostAsync<LoginResponse>("/auth/login", new
        {
            tenantId = tenantId.Trim(),
            username = username.Trim(),
            password
        }, authenticated: false, cancellationToken);
        await _session.SaveTokensAsync(response.AccessToken, response.RefreshToken);
        return response;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await PostAsync<object>("/auth/logout", new { }, authenticated: true, cancellationToken);
        }
        catch
        {
            // Offline logout still clears local tokens.
        }
        finally
        {
            _cache.ClearUserCache(_session.TenantId, _session.StoreId, _session.Username);
            _cache.ClearUserCache(_session.TenantId, string.Empty, _session.Username);
            _session.ClearTokens();
        }
    }

    public async Task<bool> RefreshSessionAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await _session.GetRefreshTokenAsync();
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        try
        {
            var response = await SendAsync<LoginResponse>(
                HttpMethod.Post,
                "/auth/refresh",
                JsonContent.Create(new { refreshToken }, options: JsonOptions),
                authenticated: false,
                cancellationToken,
                allowRefreshRetry: false);
            await _session.SaveTokensAsync(response.AccessToken, response.RefreshToken);
            return true;
        }
        catch
        {
            _session.ClearTokens();
            return false;
        }
    }


    public Task<StoresResponse> StoresAsync(CancellationToken cancellationToken = default)
        => GetCachedAsync<StoresResponse>("/stores", "stores", cancellationToken);

    public void ResetCacheStatus()
        => CacheStatus = RevenueCacheStatus.Online;

    public Task<TodayReport> TodayAsync(DateTime date, string storeId, CancellationToken cancellationToken = default)
        => GetCachedAsync<TodayReport>($"/reports/today?date={DateOnly.FromDateTime(date):yyyy-MM-dd}&storeId={Uri.EscapeDataString(storeId)}", $"today_{date:yyyyMMdd}", cancellationToken);

    public Task<MonthReport> MonthAsync(DateTime date, string storeId, CancellationToken cancellationToken = default)
        => GetCachedAsync<MonthReport>($"/reports/month?month={date:yyyy-MM}&storeId={Uri.EscapeDataString(storeId)}", $"month_{date:yyyyMM}", cancellationToken);

    public Task<RangeReport> RangeAsync(DateTime from, DateTime to, string storeId, CancellationToken cancellationToken = default)
        => GetCachedAsync<RangeReport>($"/reports/range?from={DateOnly.FromDateTime(from):yyyy-MM-dd}&to={DateOnly.FromDateTime(to):yyyy-MM-dd}&storeId={Uri.EscapeDataString(storeId)}", $"range_{from:yyyyMMdd}_{to:yyyyMMdd}", cancellationToken);

    public Task<TopProductsResponse> TopProductsAsync(DateTime from, DateTime to, string storeId, CancellationToken cancellationToken = default)
        => GetCachedAsync<TopProductsResponse>($"/reports/top-products?from={DateOnly.FromDateTime(from):yyyy-MM-dd}&to={DateOnly.FromDateTime(to):yyyy-MM-dd}&storeId={Uri.EscapeDataString(storeId)}&limit=5", $"top_{from:yyyyMMdd}_{to:yyyyMMdd}", cancellationToken);

    public Task<InventoryReportResponse> InventoryAsync(DateTime from, DateTime to, string storeId, CancellationToken cancellationToken = default)
        => GetCachedAsync<InventoryReportResponse>($"/reports/inventory?from={DateOnly.FromDateTime(from):yyyy-MM-dd}&to={DateOnly.FromDateTime(to):yyyy-MM-dd}&storeId={Uri.EscapeDataString(storeId)}", $"inventory_{from:yyyyMMdd}_{to:yyyyMMdd}", cancellationToken);

    public Task<InvoiceListResponse> InvoicesAsync(DateTime from, DateTime to, string storeId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        return GetCachedAsync<InvoiceListResponse>(
            $"/invoices?from={DateOnly.FromDateTime(from):yyyy-MM-dd}&to={DateOnly.FromDateTime(to):yyyy-MM-dd}&storeId={Uri.EscapeDataString(storeId)}&page={safePage}&pageSize={safePageSize}",
            $"invoices_{from:yyyyMMdd}_{to:yyyyMMdd}_{safePage}_{safePageSize}",
            cancellationToken);
    }

    public Task<OpenTablesReport> OpenTablesAsync(string storeId, CancellationToken cancellationToken = default)
        => GetCachedAsync<OpenTablesReport>($"/reports/open-tables?storeId={Uri.EscapeDataString(storeId)}", "open_tables", cancellationToken);

    public async Task<InvoiceDetailResponse> InvoiceDetailAsync(string invoiceId, string storeId, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await SendAsync<InvoiceDetailResponse>(HttpMethod.Get, $"/invoices/{Uri.EscapeDataString(invoiceId)}?storeId={Uri.EscapeDataString(storeId)}", null, authenticated: true, cancellationToken);
            await _cache.SaveInvoiceDetailAsync(_session.TenantId, storeId, _session.Username, invoiceId, value);
            return value;
        }
        catch (Exception ex) when (ShouldUseCacheFallback(ex, cancellationToken))
        {
            var cached = await _cache.LoadInvoiceDetailAsync(_session.TenantId, storeId, _session.Username, invoiceId);
            if (cached is not null)
            {
                MarkCache(cached.CachedAt, cached.IsStale);
                return cached.Value;
            }

            throw new InvalidOperationException(FriendlyNetworkError("Không có chi tiết hóa đơn trong cache."));
        }
    }

    private async Task<T> GetCachedAsync<T>(string path, string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            var value = await SendAsync<T>(HttpMethod.Get, path, null, authenticated: true, cancellationToken);
            await _cache.SaveAsync(_session.TenantId, _session.StoreId, _session.Username, cacheKey, value);
            return value;
        }
        catch (Exception ex) when (ShouldUseCacheFallback(ex, cancellationToken))
        {
            var cached = await _cache.LoadAsync<T>(_session.TenantId, _session.StoreId, _session.Username, cacheKey);
            if (cached is not null)
            {
                MarkCache(cached.CachedAt, cached.IsStale);
                return cached.Value;
            }

            throw new InvalidOperationException(FriendlyNetworkError("Chưa có dữ liệu cache."));
        }
    }

    private void MarkCache(DateTimeOffset cachedAt, bool isStale)
    {
        if (CacheStatus.FromCache && CacheStatus.IsStale)
        {
            return;
        }

        CacheStatus = new RevenueCacheStatus(true, isStale, cachedAt);
    }

    private Task<T> PostAsync<T>(string path, object body, bool authenticated, CancellationToken cancellationToken)
        => SendAsync<T>(HttpMethod.Post, path, JsonContent.Create(body, options: JsonOptions), authenticated, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, bool authenticated, CancellationToken cancellationToken, bool allowRefreshRetry = true)
    {
        var request = new HttpRequestMessage(method, BuildUri(path));
        request.Content = content;
        if (authenticated)
        {
            var token = await _session.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token) &&
                allowRefreshRetry &&
                await RefreshSessionAsync(cancellationToken))
            {
                token = await _session.GetAccessTokenAsync();
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException("Phiên đăng nhập đã hết hạn.");
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await _http.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (authenticated &&
                allowRefreshRetry &&
                content is null &&
                response.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
                await RefreshSessionAsync(cancellationToken))
            {
                using var retryRequest = new HttpRequestMessage(method, BuildUri(path));
                var token = await _session.GetAccessTokenAsync();
                retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var retryResponse = await _http.SendAsync(retryRequest, cancellationToken);
                var retryText = await retryResponse.Content.ReadAsStringAsync(cancellationToken);
                if (!retryResponse.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(ParseError(retryText, retryResponse.StatusCode));
                }

                if (typeof(T) == typeof(object))
                {
                    return (T)(object)new object();
                }

                return JsonSerializer.Deserialize<T>(retryText, JsonOptions)
                    ?? throw new InvalidOperationException("Cloud trả dữ liệu rỗng.");
            }

            throw new InvalidOperationException(ParseError(text, response.StatusCode));
        }

        if (typeof(T) == typeof(object))
        {
            return (T)(object)new object();
        }

        return JsonSerializer.Deserialize<T>(text, JsonOptions)
            ?? throw new InvalidOperationException("Cloud trả dữ liệu rỗng.");
    }

    private Uri BuildUri(string path)
    {
        var root = _session.WorkerUrl;
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("Chưa cấu hình Revenue Cloud URL.");
        }

        return new Uri(root.Trim().TrimEnd('/') + path, UriKind.Absolute);
    }

    private static string ParseError(string body, System.Net.HttpStatusCode statusCode)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var message) && !string.IsNullOrWhiteSpace(message.GetString()))
            {
                return message.GetString()!;
            }
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                return error.GetString() ?? $"HTTP {(int)statusCode}";
            }
        }
        catch
        {
            // Use fallback.
        }

        return string.IsNullOrWhiteSpace(body) ? $"HTTP {(int)statusCode}" : body;
    }

    private static bool ShouldUseCacheFallback(Exception ex, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return Connectivity.Current.NetworkAccess != NetworkAccess.Internet ||
               ex is HttpRequestException ||
               ex is TaskCanceledException ||
               ex is TimeoutException;
    }

    private static string FriendlyNetworkError(string suffix)
        => $"Không kết nối được Revenue Cloud. {suffix}";

    public static string Money(decimal value)
        => string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0}đ", value).Replace(",", ".");
}

public sealed record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, int RefreshExpiresIn, LoginUser User);
public sealed record LoginUser(string UserId, string TenantId, string Username, string DisplayName);
public sealed record StoresResponse(List<StoreDto> Stores);
public sealed record StoreDto(string StoreId, string Name, string Timezone, bool Enabled, DateTimeOffset? LastSyncAt);
public sealed record TodayReport(string StoreId, string Timezone, string BusinessDate, DateTimeOffset? LastSyncAt, SummaryDto Summary, List<DailyPoint> Revenue7Days, List<PaymentSlice> PaymentBreakdown);
public sealed record RangeReport(string From, string To, SummaryDto Summary, List<DailyPoint> Daily, List<PaymentSlice> PaymentBreakdown);
public sealed record MonthReport(string Month, string Timezone, SummaryDto Summary, List<DailyPoint> Daily, List<PaymentSlice> PaymentBreakdown);
public sealed record SummaryDto(decimal Revenue, int InvoiceCount, int CancelledInvoiceCount, decimal AverageInvoiceValue, decimal CashAmount, decimal TransferAmount, decimal CardAmount, decimal OtherAmount);
public sealed record DailyPoint(string Date, decimal Revenue, int InvoiceCount);
public sealed record PaymentSlice(string Method, decimal Amount);
public sealed record TopProductsResponse(string From, string To, List<TopProductDto> Items);
public sealed record TopProductDto(string ProductId, string ProductName, string ProductType, decimal Quantity, decimal Revenue);
public sealed record InventoryReportResponse(string StoreId, string From, string To, int TotalItems, List<InventoryReportItem> Items);
public sealed record InventoryReportItem(
    string BusinessDate,
    string ProductId,
    string ProductName,
    string UnitName,
    decimal OpeningQty,
    decimal ImportQty,
    decimal LastImportPrice,
    decimal ManualExportQty,
    decimal SoldQty,
    decimal TotalExportQty,
    decimal ClosingQty,
    decimal MinStock,
    DateTimeOffset? UpdatedAt);
public sealed record OpenTablesReport(string StoreId, DateTimeOffset? LastSyncAt, int TableCount, decimal EstimatedTotal, List<OpenTableDto> Tables);
public sealed record OpenTableDto(string TableId, string TableName, string ZoneId, string ZoneName, string OrderId, DateTimeOffset? OccupiedAt, decimal Total, DateTimeOffset? ModifiedAt, DateTimeOffset? SyncedAt, List<OpenTableItemDto>? Items);
public sealed record OpenTableItemDto(string LineId, string ProductId, string ProductName, string ProductType, string UnitName, decimal Quantity, decimal UnitPrice, decimal LineTotal, string Note);
public sealed record InvoiceListResponse(int Page, int PageSize, int TotalItems, List<InvoiceListItem> Items);
public sealed record InvoiceListItem(string InvoiceId, int InvoiceVersion, string Status, string BusinessDate, string TableName, string Cashier, DateTimeOffset? PaidAt, decimal Subtotal, decimal Discount, decimal Total, string PaymentMethod);
public sealed record InvoiceDetailResponse(
    string TenantId,
    string StoreId,
    string InvoiceId,
    int InvoiceVersion,
    string Status,
    string TableName,
    string Cashier,
    DateTimeOffset? OpenedAt,
    DateTimeOffset? PaidAt,
    string BusinessDate,
    decimal Subtotal,
    decimal Discount,
    decimal Total,
    string PaymentMethod,
    string DiscountNote,
    List<InvoicePaymentDto> Payments,
    List<InvoiceDetailItem> Items);
public sealed record InvoicePaymentDto(string Method, decimal Amount, DateTimeOffset? CreatedAt);
public sealed record InvoiceDetailItem(string LineId, string ProductId, string ProductName, string ProductType, string UnitName, decimal Quantity, decimal UnitPrice, decimal LineTotal, string Note);
public sealed record RevenueCacheStatus(bool FromCache, bool IsStale, DateTimeOffset? CachedAt)
{
    public static RevenueCacheStatus Online { get; } = new(false, false, null);
}
