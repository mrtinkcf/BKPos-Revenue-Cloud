using System.Security.Cryptography;
using System.Text.Json;
using BKPos.Licensing.Models;
using BKPos.Licensing.Security;

namespace BKPos.Licensing;

public sealed class LicenseValidator
{
    public LicenseValidationResult Validate(string? licenseKey, string currentHardwareId)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return LicenseValidationResult.Invalid("Chưa kích hoạt bản quyền.");
        }

        try
        {
            if (!LicenseFormat.TryNormalizeLicenseKey(licenseKey, out var normalizedLicenseKey))
            {
                return LicenseValidationResult.Invalid("License key không đúng định dạng.");
            }

            var parts = normalizedLicenseKey.Split('.');
            var isV3 = parts.Length == 3 && string.Equals(parts[0], LicenseFormat.LicenseKeyPrefix, StringComparison.OrdinalIgnoreCase);
            var isV2 = parts.Length == 3 && string.Equals(parts[0], LicenseFormat.LegacyRsaLicenseKeyPrefix, StringComparison.OrdinalIgnoreCase);
            var payloadBytes = Base64Url.Decode(isV3 || isV2 ? parts[1] : parts[0]);
            var signature = Base64Url.Decode(isV3 || isV2 ? parts[2] : parts[1]);

            if (!VerifySignature(payloadBytes, signature, isV3))
            {
                return LicenseValidationResult.Invalid("License key không hợp lệ hoặc đã bị sửa.");
            }

            var info = isV3 || isV2
                ? LicensePayloadCodec.DecodeV2(payloadBytes)
                : LicensePayloadCodec.DecodeV1(payloadBytes);

            if (!string.Equals(info.Product, BuildProductMarker(), StringComparison.Ordinal))
            {
                return LicenseValidationResult.Invalid("License key không dành cho sản phẩm này.");
            }

            if (!LicenseFormat.TryNormalizeHardwareId(info.HardwareId, out var licensedHardwareId))
            {
                return LicenseValidationResult.Invalid("Hardware ID trong license key không đúng định dạng.");
            }

            if (!string.Equals(licensedHardwareId, currentHardwareId, StringComparison.OrdinalIgnoreCase))
            {
                return LicenseValidationResult.Invalid("Phần cứng máy đã thay đổi. Vui lòng liên hệ nhà cung cấp để kích hoạt lại bản quyền.");
            }

            var now = DateTimeOffset.Now;
            if (info.IssuedAt > now.AddMinutes(10))
            {
                return LicenseValidationResult.Invalid("Ngày cấp bản quyền không hợp lệ.");
            }

            if (info.ExpiresAt is not null && info.ExpiresAt.Value < now)
            {
                return LicenseValidationResult.Invalid("Bản quyền đã hết hạn.");
            }

            return LicenseValidationResult.Valid(info);
        }
        catch (FormatException)
        {
            return LicenseValidationResult.Invalid("License key không đúng định dạng.");
        }
        catch (CryptographicException)
        {
            return LicenseValidationResult.Invalid("Không xác minh được chữ ký bản quyền.");
        }
        catch (JsonException)
        {
            return LicenseValidationResult.Invalid("Dữ liệu bản quyền không hợp lệ.");
        }
        catch (Exception ex)
        {
            return LicenseValidationResult.Invalid($"Lỗi kiểm tra bản quyền: {ex.Message}");
        }
    }

    private static string BuildProductMarker()
        => string.Concat('B', 'K', 'P', 'o', 's');

    private static bool VerifySignature(byte[] payloadBytes, byte[] signature, bool isEcdsa)
    {
        if (isEcdsa)
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(BuildEcdsaVerifierMaterial());
            return ecdsa.VerifyData(
                payloadBytes,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(BuildRsaVerifierMaterial());
        return rsa.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
    }

    private static string BuildEcdsaVerifierMaterial()
    {
        var lines = new[]
        {
            string.Concat("-----BEGIN ", "PUBLIC", " KEY-----"),
            string.Concat("MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEaqevF7T7IESht+SpbVPJF95fy1Py"),
            string.Concat("5tAvAAjCE7E+MPUeG/Yi5If+Z9dUbQaD5HUJ8RFcAzdSDbvfHeIGQ0zJqg=="),
            string.Concat("-----END ", "PUBLIC", " KEY-----")
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildRsaVerifierMaterial()
    {
        var lines = new[]
        {
            string.Concat("-----BEGIN ", "PUBLIC", " KEY-----"),
            string.Concat("MIIBojANBgkqhkiG9w0BAQEFAAOCAY8AMIIBigKCAYEAo2ZZzIC/sNXMfhdgh1nB"),
            string.Concat("FV2lB5xHekEgaek68ELQT3hDfwSmHM/ycvaChYeVCzTqjdpegqlw5koEIB2uR6jK"),
            string.Concat("WiGbSvAuXcccLJNcsSC9rDVvjQwwA4Sp4RGsediUW4cyFfJMGGE2UtDyBg9J02UD"),
            string.Concat("k3t/Ik6XZRObq6M311Zzn7XsbmltTj1+6zfxXwL4vCrDsqKjXaqtNat1VtFOvJj9"),
            string.Concat("sTSTjqqX1XFbDCtGv92mF7uXMQm324aoZ0u5e/wesihCN8o1VN5O9ZVjl8yjY030"),
            string.Concat("QIeOivUs1wiQ6X9YAn6p3Fh+UyQuXeCixrvxGMVwDs0gtwQ+xOqGL2kn08TPE6pq"),
            string.Concat("7hT/DLlISCaNah95WTAIgpiivaTmy3CNGquiU4sp22uvXWoFXxGsM+tVRXPj0V6y"),
            string.Concat("4i+6vREieYsUSvzkXfdUzxs8S+P7D8BzEScsD7hijA+7+XoMZSpwXvSMLJecqees"),
            string.Concat("ObcvNLaaImFDp46Xfuy8XFqVY2aUEw8ln4+g1FS24SgRAgMBAAE="),
            string.Concat("-----END ", "PUBLIC", " KEY-----")
        };

        return string.Join(Environment.NewLine, lines);
    }
}

