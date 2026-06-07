using System.IO.Compression;
using System.Text.Json;
using BKPos.Licensing.Models;

namespace BKPos.Licensing;

internal static class LicensePayloadCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static byte[] EncodeV2(LicenseInfo info)
    {
        var payload = new CompactPayload
        {
            S = 2,
            P = info.Product,
            I = info.LicenseId,
            N = info.CustomerName,
            T = info.Phone,
            H = info.HardwareId,
            E = info.Edition,
            F = ToFeatureFlags(info.Features),
            U = GetUnknownFeatures(info.Features),
            A = info.IssuedAt.ToUnixTimeSeconds(),
            X = info.ExpiresAt?.ToUnixTimeSeconds()
        };

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(jsonBytes);
        }

        return output.ToArray();
    }

    public static LicenseInfo DecodeV2(byte[] payloadBytes)
    {
        using var input = new MemoryStream(payloadBytes);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);

        var payload = JsonSerializer.Deserialize<CompactPayload>(output.ToArray(), JsonOptions)
                      ?? throw new JsonException("Invalid license payload.");

        return new LicenseInfo
        {
            Schema = payload.S,
            Product = payload.P,
            LicenseId = payload.I,
            CustomerName = payload.N,
            Phone = payload.T,
            HardwareId = payload.H,
            Edition = payload.E,
            Features = FromFeatureFlags(payload.F, payload.U),
            IssuedAt = DateTimeOffset.FromUnixTimeSeconds(payload.A),
            ExpiresAt = payload.X is null ? null : DateTimeOffset.FromUnixTimeSeconds(payload.X.Value)
        };
    }

    public static LicenseInfo DecodeV1(byte[] payloadBytes)
        => JsonSerializer.Deserialize<LicenseInfo>(payloadBytes, JsonOptions)
           ?? throw new JsonException("Invalid license payload.");

    private static int ToFeatureFlags(IEnumerable<string> features)
    {
        var flags = 0;
        foreach (var feature in features)
        {
            flags |= NormalizeFeature(feature) switch
            {
                "Sales" => 1,
                "Orders" => 2,
                "Reports" => 4,
                "Export" => 8,
                "Settings" => 16,
                _ => 0
            };
        }

        return flags;
    }

    private static List<string> FromFeatureFlags(int flags, IReadOnlyCollection<string>? unknownFeatures)
    {
        var features = new List<string>();
        if ((flags & 1) != 0) features.Add("Sales");
        if ((flags & 2) != 0) features.Add("Orders");
        if ((flags & 4) != 0) features.Add("Reports");
        if ((flags & 8) != 0) features.Add("Export");
        if ((flags & 16) != 0) features.Add("Settings");

        if (unknownFeatures is not null)
        {
            foreach (var feature in unknownFeatures.Select(NormalizeFeature).Where(feature => feature.Length > 0))
            {
                if (!features.Contains(feature, StringComparer.OrdinalIgnoreCase))
                {
                    features.Add(feature);
                }
            }
        }

        return features;
    }

    private static List<string>? GetUnknownFeatures(IEnumerable<string> features)
    {
        var unknown = features
            .Select(NormalizeFeature)
            .Where(feature => feature.Length > 0)
            .Where(feature => feature is not "Sales" and not "Orders" and not "Reports" and not "Export" and not "Settings")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return unknown.Count == 0 ? null : unknown;
    }

    private static string NormalizeFeature(string? feature)
        => string.IsNullOrWhiteSpace(feature) ? string.Empty : feature.Trim();

    private sealed class CompactPayload
    {
        public int S { get; set; }
        public string P { get; set; } = string.Empty;
        public string I { get; set; } = string.Empty;
        public string N { get; set; } = string.Empty;
        public string T { get; set; } = string.Empty;
        public string H { get; set; } = string.Empty;
        public string E { get; set; } = string.Empty;
        public int F { get; set; }
        public List<string>? U { get; set; }
        public long A { get; set; }
        public long? X { get; set; }
    }
}
