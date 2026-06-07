namespace BKPos.Licensing.Security;

internal static class Base32Crockford
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Encode(byte[] data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        var output = new char[(data.Length * 8 + 4) / 5];
        var buffer = 0;
        var bitsLeft = 0;
        var index = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                output[index++] = Alphabet[(buffer >> (bitsLeft - 5)) & 31];
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            output[index++] = Alphabet[(buffer << (5 - bitsLeft)) & 31];
        }

        return new string(output, 0, index);
    }

    public static string Group(string value, int groupSize = 5)
        => string.Join('-', value.Chunk(groupSize).Select(chars => new string(chars)));
}
