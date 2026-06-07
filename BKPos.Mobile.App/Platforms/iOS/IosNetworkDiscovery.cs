using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using BKPos.Core.Interfaces;

namespace BKPos.Mobile.App.Platforms.iOS;

public sealed class IosNetworkDiscovery : INetworkDiscovery
{
    private const int DiscoveryPort = 5051;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async IAsyncEnumerable<DiscoveredServer> DiscoverAsync(
        TimeSpan timeout,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!timeoutCts.Token.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            if (!TryParseAdvertisement(result.Buffer, out var server))
            {
                continue;
            }

            var key = $"{server.Ip}:{server.Port}";
            if (seen.Add(key))
            {
                yield return server;
            }
        }
    }

    private static bool TryParseAdvertisement(byte[] bytes, out DiscoveredServer server)
    {
        server = new DiscoveredServer(string.Empty, string.Empty, 0, string.Empty, string.Empty, string.Empty, DateTimeOffset.MinValue);

        try
        {
            var payload = JsonSerializer.Deserialize<AgentAdvertisementPayload>(Encoding.UTF8.GetString(bytes), JsonOptions);
            if (payload is null
                || !string.Equals(payload.Service, "bkpos", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(payload.Ip)
                || payload.Port <= 0)
            {
                return false;
            }

            var url = string.IsNullOrWhiteSpace(payload.Url)
                ? $"http://{payload.Ip}:{payload.Port}"
                : payload.Url.Trim().TrimEnd('/');

            server = new DiscoveredServer(
                string.IsNullOrWhiteSpace(payload.Name) ? payload.Ip.Trim() : payload.Name.Trim(),
                payload.Ip.Trim(),
                payload.Port,
                url,
                string.IsNullOrWhiteSpace(payload.Deeplink) ? $"bkpos://{payload.Ip}:{payload.Port}" : payload.Deeplink.Trim(),
                string.IsNullOrWhiteSpace(payload.Version) ? "1.0" : payload.Version.Trim(),
                payload.Timestamp == default ? DateTimeOffset.Now : payload.Timestamp);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class AgentAdvertisementPayload
    {
        public string? Service { get; set; }

        public string? Version { get; set; }

        public string? Name { get; set; }

        public string? Ip { get; set; }

        public int Port { get; set; }

        public string? Url { get; set; }

        public string? Deeplink { get; set; }

        public DateTimeOffset Timestamp { get; set; }
    }
}
