using BKPos.Licensing.Security;

namespace BKPos.Licensing;

public static class HardwareIdFormatter
{
    public static string CreateFromRawId(string rawFingerprint)
    {
        if (string.IsNullOrWhiteSpace(rawFingerprint))
        {
            throw new ArgumentException("Raw fingerprint is required.", nameof(rawFingerprint));
        }

        var normalized = rawFingerprint.Trim().ToUpperInvariant();
        var hash = Hashing.Sha256Bytes(normalized);
        return Base32Crockford.Group(Base32Crockford.Encode(hash)[..25]);
    }
}
