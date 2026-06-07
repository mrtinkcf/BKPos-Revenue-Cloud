using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Storage;

namespace BKPos.Mobile.App.Services;

public sealed class ApiClient
{
    private const string ServerUrlKey = "server_url";
    private const string AccessTokenKey = "access_token";
    private const string RefreshTokenKey = "refresh_token";
    private static readonly string LicenseWorkerUrl = string.Concat("https://", "bkpos", "-lic", "-bk", ".", "phongnt031989", ".", "workers", ".", "dev");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string? ServerUrl
    {
        get
        {
            var value = Preferences.Default.Get(ServerUrlKey, string.Empty);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        private set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Preferences.Default.Remove(ServerUrlKey);
            }
            else
            {
                Preferences.Default.Set(ServerUrlKey, value.Trim().TrimEnd('/'));
            }
        }
    }

    public async Task SaveServerUrlAsync(string serverUrl)
    {
        ServerUrl = NormalizeServerUrl(serverUrl);
        await SecureStorage.Default.SetAsync(ServerUrlKey, ServerUrl);
    }

    public async Task<ServerInfoDto> GetServerInfoAsync(CancellationToken cancellationToken = default)
        => await SendAsync<ServerInfoDto>(HttpMethod.Get, "/server/info", authRequired: false, cancellationToken: cancellationToken);

    public async Task<LoginResponseDto> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<LoginResponseDto>(
            HttpMethod.Post,
            "/auth/login",
            new LoginRequestDto(username, password),
            authRequired: false,
            cancellationToken);

        await SecureStorage.Default.SetAsync(AccessTokenKey, response.AccessToken);
        await SecureStorage.Default.SetAsync(RefreshTokenKey, response.RefreshToken);
        return response;
    }

    public async Task<LicenseStatusDto> GetLicenseStatusAsync(CancellationToken cancellationToken = default)
        => await SendAsync<LicenseStatusDto>(HttpMethod.Get, "/license/status", authRequired: false, cancellationToken: cancellationToken);

    public async Task<LicenseActivateResponseDto> ActivateLicenseAsync(string licenseKey, string hardwareId, CancellationToken cancellationToken = default)
        => await SendAsync<LicenseActivateResponseDto>(
            HttpMethod.Post,
            "/license/activate",
            new ActivateLicenseRequestDto(licenseKey, hardwareId),
            authRequired: false,
            cancellationToken);

    public async Task<LicenseActivateResponseDto> ClaimLicenseAsync(string hardwareId, CancellationToken cancellationToken = default)
        => await SendAsync<LicenseActivateResponseDto>(
            HttpMethod.Post,
            "/license/claim",
            new ClaimLicenseRequestDto(hardwareId),
            authRequired: false,
            cancellationToken);

    public async Task<CloudLicenseRequestSubmitResponseDto> SubmitCloudLicenseRequestAsync(
        CloudLicenseRequestSubmitDto payload,
        CancellationToken cancellationToken = default)
        => await SendAsync<CloudLicenseRequestSubmitResponseDto>(
            HttpMethod.Post,
            LicenseWorkerUrl + "/license-request",
            payload,
            authRequired: false,
            cancellationToken);

    public async Task<CloudLicenseRequestStatusResponseDto> GetCloudLicenseRequestStatusAsync(
        string requestId,
        string requestSecret,
        CancellationToken cancellationToken = default)
        => await SendAsync<CloudLicenseRequestStatusResponseDto>(
            HttpMethod.Post,
            LicenseWorkerUrl + "/license-request/status",
            new CloudLicenseRequestStatusDto(requestId, requestSecret),
            authRequired: false,
            cancellationToken);

    public async Task<IReadOnlyList<ZoneDto>> GetZonesAsync(CancellationToken cancellationToken = default)
        => (await SendAsync<ZonesResponseDto>(HttpMethod.Get, "/zones", cancellationToken: cancellationToken)).Zones;

    public async Task<IReadOnlyList<TableDto>> GetTablesAsync(string? zoneId = null, CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(zoneId)
            ? "/tables"
            : $"/tables?zoneId={Uri.EscapeDataString(zoneId)}";
        return (await SendAsync<TablesResponseDto>(HttpMethod.Get, path, cancellationToken: cancellationToken)).Tables;
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? categoryId = null, CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(categoryId)
            ? "/catalog/products"
            : $"/catalog/products?categoryId={Uri.EscapeDataString(categoryId)}";
        return (await SendAsync<ProductsResponseDto>(HttpMethod.Get, path, cancellationToken: cancellationToken)).Products;
    }

    public async Task<OrderEnvelopeDto> OpenTableAsync(string tableId, CancellationToken cancellationToken = default)
        => await SendAsync<OrderEnvelopeDto>(
            HttpMethod.Post,
            "/orders/open-table",
            new OpenTableRequestDto(null, tableId, null),
            cancellationToken: cancellationToken);

    public async Task<OrderEnvelopeDto> GetOrderByTableAsync(string tableId, CancellationToken cancellationToken = default)
        => await SendAsync<OrderEnvelopeDto>(HttpMethod.Get, $"/orders/by-table/{Uri.EscapeDataString(tableId)}", cancellationToken: cancellationToken);

    public async Task<OrderDto> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
        => await SendAsync<OrderDto>(HttpMethod.Get, $"/orders/{Uri.EscapeDataString(orderId)}", cancellationToken: cancellationToken);

    public async Task<OrderVersionDto> GetOrderVersionAsync(string orderId, CancellationToken cancellationToken = default)
        => await SendAsync<OrderVersionDto>(HttpMethod.Get, $"/orders/{Uri.EscapeDataString(orderId)}/version", cancellationToken: cancellationToken);

    public async Task<MutationResponseDto> AddLineAsync(string orderId, ProductDto product, int quantity, CancellationToken cancellationToken = default)
        => await AddLineAsync(orderId, product, quantity, null, cancellationToken);

    public async Task<MutationResponseDto> AddLineAsync(string orderId, ProductDto product, int quantity, string? note, CancellationToken cancellationToken = default)
        => await SendAsync<MutationResponseDto>(
            HttpMethod.Post,
            $"/orders/{Uri.EscapeDataString(orderId)}/lines",
            new AddOrderLineRequestDto(product.ExternalId, product.Name, quantity, product.Price, note),
            cancellationToken: cancellationToken);

    public async Task<MutationResponseDto> UpdateLineAsync(string orderId, string lineId, int quantity, string? note, CancellationToken cancellationToken = default)
        => await SendAsync<MutationResponseDto>(
            HttpMethod.Patch,
            $"/orders/{Uri.EscapeDataString(orderId)}/lines/{Uri.EscapeDataString(lineId)}",
            new UpdateOrderLineRequestDto(quantity, note),
            cancellationToken: cancellationToken);

    public async Task<MutationResponseDto> RemoveLineAsync(string orderId, string lineId, CancellationToken cancellationToken = default)
        => await SendAsync<MutationResponseDto>(
            HttpMethod.Delete,
            $"/orders/{Uri.EscapeDataString(orderId)}/lines/{Uri.EscapeDataString(lineId)}",
            cancellationToken: cancellationToken);

    public async Task<JsonElement> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
        => await SendAsync<JsonElement>(
            HttpMethod.Post,
            $"/orders/{Uri.EscapeDataString(orderId)}/cancel",
            cancellationToken: cancellationToken);

    public async Task<MutationResponseDto> TransferOrderAsync(string orderId, string targetTableId, CancellationToken cancellationToken = default)
        => await SendAsync<MutationResponseDto>(
            HttpMethod.Post,
            $"/orders/{Uri.EscapeDataString(orderId)}/transfer",
            new TransferOrderRequestDto(null, targetTableId),
            cancellationToken: cancellationToken);

    public async Task<MutationResponseDto> SplitOrderAsync(string orderId, string targetTableId, IReadOnlyList<string> lineIds, CancellationToken cancellationToken = default)
        => await SendAsync<MutationResponseDto>(
            HttpMethod.Post,
            $"/orders/{Uri.EscapeDataString(orderId)}/split",
            new SplitOrderRequestDto(null, targetTableId, lineIds),
            cancellationToken: cancellationToken);

    public async Task<MutationResponseDto> MergeOrderAsync(string orderId, string targetOrderId, CancellationToken cancellationToken = default)
        => await SendAsync<MutationResponseDto>(
            HttpMethod.Post,
            $"/orders/{Uri.EscapeDataString(orderId)}/merge",
            new MergeOrderRequestDto(targetOrderId),
            cancellationToken: cancellationToken);

    public async Task<PaymentResponseDto> PayOrderAsync(
        string orderId,
        IReadOnlyList<PaymentLineDto> payments,
        decimal discountAmount,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
        => await SendAsync<PaymentResponseDto>(
            HttpMethod.Post,
            $"/orders/{Uri.EscapeDataString(orderId)}/pay",
            new PayOrderRequestDto(payments, discountAmount, idempotencyKey),
            cancellationToken: cancellationToken);

    public async Task<PrintResponseDto> PrintOrderAsync(string orderId, string printType, CancellationToken cancellationToken = default)
        => await SendAsync<PrintResponseDto>(
            HttpMethod.Post,
            $"/orders/{Uri.EscapeDataString(orderId)}/print/{Uri.EscapeDataString(printType)}",
            new PrintOrderRequestDto(1),
            cancellationToken: cancellationToken);

    public async Task<PrintersResponseDto> GetPrintersAsync(CancellationToken cancellationToken = default)
        => await SendAsync<PrintersResponseDto>(HttpMethod.Get, "/server/printers", cancellationToken: cancellationToken);

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                await SendAsync<JsonElement>(
                    HttpMethod.Post,
                    "/auth/logout",
                    new RefreshTokenRequestDto(refreshToken),
                    authRequired: false,
                    cancellationToken);
            }
        }
        finally
        {
            SecureStorage.Default.Remove(AccessTokenKey);
            SecureStorage.Default.Remove(RefreshTokenKey);
        }
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? payload = null,
        bool authRequired = true,
        CancellationToken cancellationToken = default)
    {
        using var request = await BuildRequestAsync(method, path, payload, authRequired);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            using var retry = await BuildRequestAsync(method, path, payload, authRequired);
            using var retryResponse = await _httpClient.SendAsync(retry, cancellationToken);
            return await ReadResponseAsync<T>(retryResponse, cancellationToken);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized && authRequired && await RefreshTokenAsync(cancellationToken))
        {
            using var retry = await BuildRequestAsync(method, path, payload, authRequired);
            using var retryResponse = await _httpClient.SendAsync(retry, cancellationToken);
            return await ReadResponseAsync<T>(retryResponse, cancellationToken);
        }

        return await ReadResponseAsync<T>(response, cancellationToken);
    }

    private async Task<HttpRequestMessage> BuildRequestAsync(HttpMethod method, string path, object? payload, bool authRequired)
    {
        var request = new HttpRequestMessage(method, BuildRequestUri(path));
        if (payload is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        }

        if (authRequired)
        {
            var accessToken = await SecureStorage.Default.GetAsync(AccessTokenKey);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        return request;
    }

    private Uri BuildRequestUri(string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute;
        }

        var serverUrl = ServerUrl;
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            throw new ApiException(HttpStatusCode.BadRequest, "missing_server_url");
        }

        var baseUri = new Uri(serverUrl.TrimEnd('/') + "/");
        return new Uri(baseUri, path.TrimStart('/'));
    }

    private async Task<bool> RefreshTokenAsync(CancellationToken cancellationToken)
    {
        var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        try
        {
            var refreshed = await SendAsync<RefreshResponseDto>(
                HttpMethod.Post,
                "/auth/refresh",
                new RefreshTokenRequestDto(refreshToken),
                authRequired: false,
                cancellationToken);

            await SecureStorage.Default.SetAsync(AccessTokenKey, refreshed.AccessToken);
            return true;
        }
        catch
        {
            SecureStorage.Default.Remove(AccessTokenKey);
            SecureStorage.Default.Remove(RefreshTokenKey);
            return false;
        }
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = TryReadError(body);
            throw new ApiException(response.StatusCode, error.Code, error.Message);
        }

        var result = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return result ?? throw new ApiException(response.StatusCode, "empty_response");
    }

    private static string NormalizeServerUrl(string value)
    {
        var raw = value.Trim().TrimEnd('/');
        var url = raw;
        if (url.StartsWith("bkpos://", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url["bkpos://".Length..];
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        var builder = new UriBuilder(uri);
        if (!HasExplicitPort(url))
        {
            builder.Port = 5050;
        }

        return builder.Uri.ToString().TrimEnd('/');
    }

    private static bool HasExplicitPort(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Authority.EndsWith($":{uri.Port}", StringComparison.Ordinal);
    }

    private static ApiError TryReadError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new ApiError("request_failed", null);
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new ApiError("request_failed", body);
            }

            var code = doc.RootElement.TryGetProperty("error", out var error)
                ? error.GetString()
                : null;
            var message = doc.RootElement.TryGetProperty("message", out var detail)
                ? detail.GetString()
                : null;
            var status = doc.RootElement.TryGetProperty("status", out var statusValue)
                ? statusValue.GetString()
                : null;

            return new ApiError(
                string.IsNullOrWhiteSpace(code) ? status ?? "request_failed" : code,
                string.IsNullOrWhiteSpace(message) ? null : message);
        }
        catch
        {
            return new ApiError("request_failed", body);
        }
    }

    private sealed record ApiError(string Code, string? Message);
}

public sealed class ApiException : Exception
{
    public ApiException(HttpStatusCode statusCode, string error, string? detail = null)
        : base(detail ?? error)
    {
        StatusCode = statusCode;
        Error = error;
        Detail = detail;
    }

    public HttpStatusCode StatusCode { get; }

    public string Error { get; }

    public string? Detail { get; }
}

public sealed record LoginRequestDto(string Username, string Password);

public sealed record LoginResponseDto(bool Ok, string AccessToken, string RefreshToken, int ExpiresIn, DateTimeOffset ExpiresAt);

public sealed record RefreshTokenRequestDto(string RefreshToken);

public sealed record RefreshResponseDto(bool Ok, string AccessToken, int ExpiresIn, DateTimeOffset ExpiresAt);

public sealed record ActivateLicenseRequestDto(string LicenseKey, string HardwareId);

public sealed record ClaimLicenseRequestDto(string HardwareId);

public sealed record LicenseActivateResponseDto(bool Ok, LicenseStatusDto License);

public sealed record LicenseStatusDto(bool Ok, string Status, string? LicenseId, string? Edition, DateTimeOffset? ExpiresAt, string? HardwareId, string? Message);

public sealed record CloudLicenseRequestSubmitDto(
    string ProductType,
    string HardwareId,
    string CustomerName,
    string Phone,
    string StoreName,
    string Address,
    string ContactName,
    string AppVersion,
    string DeviceName,
    string Note);

public sealed record CloudLicenseRequestStatusDto(string RequestId, string RequestSecret);

public sealed record CloudLicenseRequestSubmitResponseDto(bool Ok, CloudLicenseRequestDto? Request, string? RequestSecret);

public sealed record CloudLicenseRequestStatusResponseDto(bool Ok, CloudLicenseRequestDto? Request, CloudLicenseDto? License);

public sealed record CloudLicenseRequestDto(
    string RequestId,
    string ProductType,
    string HardwareId,
    string CustomerName,
    string Phone,
    string StoreName,
    string Status,
    string? LicenseId,
    string? RejectionReason);

public sealed record CloudLicenseDto(string? LicenseId, string? LicenseKey, string? Status);

public sealed record ServerInfoDto(bool Ok, string Name, string Version, string BindIp, int Port, bool DbConnected, int PrinterCount);

public sealed record ZonesResponseDto(IReadOnlyList<ZoneDto> Zones);

public sealed record ZoneDto(int Id, string ExternalId, string Name);

public sealed record TablesResponseDto(IReadOnlyList<TableDto> Tables);

public sealed record TableDto(
    string TableId,
    int TableHashId,
    string TableName,
    string ZoneId,
    int ZoneHashId,
    bool HasOpenOrder,
    string? OrderId,
    DateTime? OccupiedAt,
    decimal Total);

public sealed record ProductsResponseDto(IReadOnlyList<ProductDto> Products);

public sealed record ProductDto(int Id, string ExternalId, string Name, string UnitName, decimal Price, int ProductType, string? ProductTypeName, int CategoryId, string CategoryExternalId, string CategoryName);

public sealed record OpenTableRequestDto(string? TableId, string? TableExternalId, string? Note);

public sealed record OrderEnvelopeDto(bool Ok, string OrderId, OrderDto? Order);

public sealed record OrderDto(
    string OrderId,
    string TableId,
    DateTime? CreatedAt,
    string? Note,
    string UserId,
    decimal Total,
    bool IsPaid,
    DateTime? ModifiedAt,
    IReadOnlyList<OrderLineDto> Lines);

public sealed record OrderLineDto(
    string Id,
    string ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? Note,
    int ProductType = 1,
    int KitchenPrintedQuantity = 0,
    int KitchenPendingQuantity = 0,
    bool IsKitchenPrinted = false,
    bool HasKitchenPrint = false);

public sealed record OrderVersionDto(string OrderId, long Version, DateTime? ModifiedAt, bool IsPaid);

public sealed record AddOrderLineRequestDto(string ProductId, string ProductName, int Quantity, decimal UnitPrice, string? Note);

public sealed record UpdateOrderLineRequestDto(int? Quantity, string? Note);

public sealed record MutationResponseDto(bool Ok, string OrderId, decimal Total, DateTime? ModifiedAt, OrderVersionDto? Version);

public sealed record TransferOrderRequestDto(string? TargetTableId, string? TargetTableExternalId);

public sealed record SplitOrderRequestDto(string? TargetTableId, string? TargetTableExternalId, IReadOnlyList<string> LineIds);

public sealed record MergeOrderRequestDto(string TargetOrderId);

public sealed record PayOrderRequestDto(IReadOnlyList<PaymentLineDto> Payments, decimal DiscountAmount, string IdempotencyKey, decimal? CashReceived = null);

public sealed record PaymentLineDto(string Method, decimal Amount);

public sealed record PaymentResponseDto(
    bool Ok,
    string ReceiptNumber,
    string OrderId,
    decimal Subtotal,
    decimal Total,
    decimal DiscountAmount,
    decimal EligibleDiscountSubtotal,
    decimal DiscountPercent,
    decimal PaidAmount,
    decimal Change,
    DateTimeOffset PaidAt,
    IReadOnlyList<PaymentLineDto> Payments,
    string? IncomeWarning);

public sealed record PrintOrderRequestDto(int Quantity);

public sealed record PrintResponseDto(bool Ok, string JobId, string OrderId, string JobType, string Printer, bool Printed, bool Deduplicated, DateTimeOffset PrintedAt);

public sealed record PrintersResponseDto(IReadOnlyList<PrinterRouteDto> Printers, IReadOnlyList<PrinterProfileDto> Profiles);

public sealed record PrinterRouteDto(string Role, bool Enabled, string? Id, string Name, string PrinterName, bool IsOnline);

public sealed record PrinterProfileDto(string Id, string Name, string PrinterName, string ConnectionType, bool IsDefault, bool IsOnline);
