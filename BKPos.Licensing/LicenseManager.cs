using BKPos.Licensing.Models;

namespace BKPos.Licensing;

public sealed class LicenseManager
{
    private readonly LicenseStorage _storage;
    private readonly LicenseValidator _validator;
    private readonly string _hardwareId;
    private readonly ILicenseRevocationChecker? _revocationChecker;
    private readonly ILicenseActivationReporter? _activationReporter;

    public LicenseManager(
        HardwareIdProvider hardwareIdProvider,
        LicenseStorage storage,
        LicenseValidator validator,
        ILicenseRevocationChecker? revocationChecker = null,
        ILicenseActivationReporter? activationReporter = null)
    {
        _storage = storage;
        _validator = validator;
        _revocationChecker = revocationChecker;
        _activationReporter = activationReporter;
        _hardwareId = hardwareIdProvider.GetHardwareId();
        Refresh();
    }

    public LicenseValidationResult Current { get; private set; } = LicenseValidationResult.Invalid("Chưa kiểm tra bản quyền.");

    public string HardwareId => _hardwareId;

    public bool IsActivated => Current.IsValid;

    public event Action? StateChanged;

    public LicenseValidationResult Refresh()
    {
        Current = ApplyRevocation(_validator.Validate(_storage.Load(), HardwareId));
        StateChanged?.Invoke();
        return Current;
    }

    public LicenseValidationResult Activate(string licenseKey)
        => ActivateCoreAsync(licenseKey, reportOnline: false).GetAwaiter().GetResult();

    public Task<LicenseValidationResult> ActivateAsync(string licenseKey, CancellationToken cancellationToken = default)
        => ActivateCoreAsync(licenseKey, reportOnline: true, cancellationToken);

    private async Task<LicenseValidationResult> ActivateCoreAsync(
        string licenseKey,
        bool reportOnline,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = LicenseFormat.TryNormalizeLicenseKey(licenseKey, out var normalized)
            ? normalized
            : licenseKey;
        var result = ApplyRevocation(_validator.Validate(normalizedKey, HardwareId));
        if (result.IsValid && reportOnline && _activationReporter is not null && result.Info is not null)
        {
            var report = await _activationReporter
                .ReportActivationAsync(result.Info, normalizedKey, HardwareId, cancellationToken)
                .ConfigureAwait(false);
            if (report.ShouldBlock)
            {
                return LicenseValidationResult.Revoked(report.Message ?? "Bản quyền không còn hợp lệ.");
            }
        }

        if (result.IsValid)
        {
            _storage.Save(normalizedKey.Trim());
            Current = result;
            StateChanged?.Invoke();
        }

        return result;
    }

    public void Clear()
    {
        _storage.Clear();
        Current = LicenseValidationResult.Invalid("Đã xóa thông tin kích hoạt.");
        StateChanged?.Invoke();
    }

    public bool HasFeature(string feature)
    {
        if (!Current.IsValid || Current.Info is null)
        {
            return false;
        }

        return Current.Info.Features.Any(item => string.Equals(item, feature, StringComparison.OrdinalIgnoreCase));
    }

    private LicenseValidationResult ApplyRevocation(LicenseValidationResult result)
    {
        if (!result.IsValid || result.Info is null || string.IsNullOrWhiteSpace(result.Info.LicenseId))
        {
            return result;
        }

        return _revocationChecker?.IsRevoked(result.Info.LicenseId) == true
            ? LicenseValidationResult.Revoked("Bản quyền của bạn đã bị thu hồi. Vui lòng liên hệ Bảo Khang Laptop ZALO: 0396 529 103 hoặc 0919 529 103.")
            : result;
    }
}
