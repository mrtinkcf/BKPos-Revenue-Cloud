using System.Security.Cryptography;
using System.Text;

namespace BKPos.Licensing.Security;

internal static class Hashing
{
    public static byte[] Sha256Bytes(string value)
        => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    public static string Sha256Hex(string value)
        => Convert.ToHexString(Sha256Bytes(value)).ToLowerInvariant();
}
