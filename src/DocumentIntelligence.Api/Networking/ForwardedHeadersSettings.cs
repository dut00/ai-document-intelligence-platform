namespace DocumentIntelligence.Api.Networking;

/// <summary>
/// Reverse proxies whose X-Forwarded-For and X-Forwarded-Proto headers the API believes. Requests
/// from any other address keep their own connection address, so a client cannot pick the IP address
/// that the per-IP rate limit sees.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>
    /// Proxy IP addresses, e.g. <c>10.0.0.5</c>. Loopback is always trusted.
    /// </summary>
    public IReadOnlyList<string> KnownProxies { get; init; } = [];

    /// <summary>
    /// Proxy networks in CIDR notation, e.g. <c>172.16.0.0/12</c>.
    /// </summary>
    public IReadOnlyList<string> KnownNetworks { get; init; } = [];
}
