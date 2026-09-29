using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace DocumentIntelligence.IntegrationTests.Infrastructure;

/// <summary>
/// The in-memory test server has no client IP address, so every request would share one rate limit
/// partition. This gives each request a random address, unless it pins one with <see cref="HeaderName"/>.
/// </summary>
public sealed class TestClientIpStartupFilter : IStartupFilter
{
    public const string HeaderName = "X-Test-Client-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = context.Request.Headers.TryGetValue(HeaderName, out var address)
                    ? IPAddress.Parse(address.ToString())
                    : new IPAddress(Guid.NewGuid().ToByteArray());

                return nextMiddleware(context);
            });

            next(app);
        };
}
