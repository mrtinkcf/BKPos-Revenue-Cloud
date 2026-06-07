namespace BKPos.Core.Interfaces;

public interface INetworkDiscovery
{
    IAsyncEnumerable<DiscoveredServer> DiscoverAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

public sealed record DiscoveredServer(
    string Name,
    string Ip,
    int Port,
    string Url,
    string Deeplink,
    string Version,
    DateTimeOffset SeenAt);
