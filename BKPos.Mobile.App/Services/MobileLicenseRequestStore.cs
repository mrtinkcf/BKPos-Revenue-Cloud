using Microsoft.Maui.Storage;

namespace BKPos.Mobile.App.Services;

internal static class MobileLicenseRequestStore
{
    private const string RequestIdKey = "mobile_license_request_id";
    private const string RequestSecretKey = "mobile_license_request_secret";
    private const string ProductTypeKey = "mobile_license_request_product_type";

    public static async Task SaveAsync(string requestId, string requestSecret, string productType)
    {
        await SecureStorage.Default.SetAsync(RequestIdKey, requestId);
        await SecureStorage.Default.SetAsync(RequestSecretKey, requestSecret);
        await SecureStorage.Default.SetAsync(ProductTypeKey, productType);
    }

    public static async Task<PendingMobileLicenseRequest?> LoadAsync()
    {
        var requestId = await SecureStorage.Default.GetAsync(RequestIdKey);
        var requestSecret = await SecureStorage.Default.GetAsync(RequestSecretKey);
        var productType = await SecureStorage.Default.GetAsync(ProductTypeKey);
        if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(requestSecret))
        {
            return null;
        }

        return new PendingMobileLicenseRequest(
            requestId.Trim(),
            requestSecret.Trim(),
            string.IsNullOrWhiteSpace(productType) ? "Mobile" : productType.Trim());
    }

    public static void Clear()
    {
        SecureStorage.Default.Remove(RequestIdKey);
        SecureStorage.Default.Remove(RequestSecretKey);
        SecureStorage.Default.Remove(ProductTypeKey);
    }
}

internal sealed record PendingMobileLicenseRequest(string RequestId, string RequestSecret, string ProductType);
