using BKPos.Licensing.Models;

namespace BKPos.Licensing.Models;

public sealed class LicenseValidationResult
{
    public bool IsValid { get; init; }
    public bool IsRevoked { get; init; }
    public string Message { get; init; } = string.Empty;
    public LicenseInfo? Info { get; init; }

    public static LicenseValidationResult Valid(LicenseInfo info) => new()
    {
        IsValid = true,
        Message = "Đã kích hoạt",
        Info = info
    };

    public static LicenseValidationResult Invalid(string message) => new()
    {
        IsValid = false,
        Message = message
    };

    public static LicenseValidationResult Revoked(string message) => new()
    {
        IsValid = false,
        IsRevoked = true,
        Message = message
    };
}
