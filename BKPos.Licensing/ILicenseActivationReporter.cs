using BKPos.Licensing.Models;

namespace BKPos.Licensing;

public interface ILicenseActivationReporter
{
    Task<LicenseActivationReportResult> ReportActivationAsync(
        LicenseInfo info,
        string licenseKey,
        string hardwareId,
        CancellationToken cancellationToken = default);
}

public sealed record LicenseActivationReportResult(bool ShouldBlock, string? Message)
{
    public static LicenseActivationReportResult Accepted() => new(false, null);

    public static LicenseActivationReportResult Blocked(string message) => new(true, message);
}
