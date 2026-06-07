using System.Text.RegularExpressions;

namespace BKPos.Licensing;

public static partial class LicenseFormat
{
    public const string HardwareIdExample = "017D0-DHF7K-8JFN8-A9QCF-BTXF9";
    public const string LicenseKeyPrefix = "BKP3";
    public const string LegacyRsaLicenseKeyPrefix = "BKP2";

    private const int HardwareIdLength = 25;
    private const int HardwareIdGroupSize = 5;
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static bool TryNormalizeHardwareId(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var compact = string.Concat(value
            .Trim()
            .ToUpperInvariant()
            .Where(ch => !char.IsWhiteSpace(ch) && ch != '-'));

        if (compact.Length != HardwareIdLength)
        {
            return false;
        }

        if (compact.Any(ch => !CrockfordAlphabet.Contains(ch)))
        {
            return false;
        }

        normalized = string.Join('-', compact.Chunk(HardwareIdGroupSize).Select(chars => new string(chars)));
        return true;
    }

    public static bool TryNormalizeLicenseKey(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var compact = WhitespaceRegex().Replace(value.Trim(), string.Empty);
        var parts = compact.Split('.');
        if (parts.Length == 3
            && (string.Equals(parts[0], LicenseKeyPrefix, StringComparison.OrdinalIgnoreCase)
                || string.Equals(parts[0], LegacyRsaLicenseKeyPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            if (!IsBase64UrlPart(parts[1]) || !IsBase64UrlPart(parts[2]))
            {
                return false;
            }

            normalized = string.Join('.', parts[0].ToUpperInvariant(), parts[1], parts[2]);
            return true;
        }

        // Backward compatibility for keys generated before BKP2.
        if (parts.Length == 2 && IsBase64UrlPart(parts[0]) && IsBase64UrlPart(parts[1]))
        {
            normalized = compact;
            return true;
        }

        return false;
    }

    private static bool IsBase64UrlPart(string value)
        => value.Length > 0 && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
