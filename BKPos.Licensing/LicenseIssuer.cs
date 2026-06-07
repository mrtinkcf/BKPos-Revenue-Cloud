using System.Security.Cryptography;
using BKPos.Licensing.Models;
using BKPos.Licensing.Security;

namespace BKPos.Licensing;

public sealed class LicenseIssuer
{
    private readonly string _privateKeyPem;

    public LicenseIssuer(string privateKeyPem)
    {
        _privateKeyPem = privateKeyPem;
    }

    public string CreateLicenseKey(LicenseInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.LicenseId))
        {
            info.LicenseId = "BKP-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        }

        if (!LicenseFormat.TryNormalizeHardwareId(info.HardwareId, out var hardwareId))
        {
            throw new InvalidOperationException($"Hardware ID không đúng định dạng. Dạng đúng: {LicenseFormat.HardwareIdExample}");
        }

        if (info.IssuedAt == default)
        {
            info.IssuedAt = DateTimeOffset.Now;
        }

        if (info.Features.Count == 0)
        {
            info.Features.AddRange(["Sales", "Orders", "Reports", "Export", "Settings"]);
        }

        info.Product = string.IsNullOrWhiteSpace(info.Product) ? "BKPos" : info.Product.Trim();
        info.HardwareId = hardwareId;
        info.LicenseId = info.LicenseId.Trim().ToUpperInvariant();

        var payloadBytes = LicensePayloadCodec.EncodeV2(info);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(_privateKeyPem);
        var signature = ecdsa.SignData(
            payloadBytes,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return string.Join('.', LicenseFormat.LicenseKeyPrefix, Base64Url.Encode(payloadBytes), Base64Url.Encode(signature));
    }
}
