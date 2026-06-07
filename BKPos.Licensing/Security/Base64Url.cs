namespace BKPos.Licensing.Security;

internal static class Base64Url
{
    public static string Encode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        if (padded.Length % 4 == 1)
        {
            throw new FormatException("Invalid Base64Url length.");
        }

        var padding = (4 - padded.Length % 4) % 4;
        padded += padding switch
        {
            1 => "=",
            2 => "==",
            _ => string.Empty
        };
        return Convert.FromBase64String(padded);
    }
}
