using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace DocumentIntelligence.Api.Networking;

internal static class ForwardedHeadersExtensions
{
    /// <summary>
    /// Behind a reverse proxy every request arrives from the proxy's address; this restores the
    /// client's address from X-Forwarded-For, but only for the proxies listed in <see cref="ForwardedHeadersSettings"/>.
    /// </summary>
    public static IServiceCollection AddTrustedForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        // Read once at startup: the middleware takes its options when the pipeline is built.
        var settings = configuration.GetSection(ForwardedHeadersSettings.SectionName).Get<ForwardedHeadersSettings>()
            ?? new ForwardedHeadersSettings();

        var proxies = settings.KnownProxies.Select(IPAddress.Parse).ToList();
        var networks = settings.KnownNetworks.Select(network => System.Net.IPNetwork.Parse(network)).ToList();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Only the entry the nearest proxy appended; anything further left came from the client.
            options.ForwardLimit = 1;

            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
            }

            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });

        return services;
    }
}
