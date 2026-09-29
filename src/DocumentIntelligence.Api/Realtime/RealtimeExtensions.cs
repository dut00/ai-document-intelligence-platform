using System.Text.Json.Serialization;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace DocumentIntelligence.Api.Realtime;

internal static class RealtimeExtensions
{
    private const string AccessTokenParameter = "access_token";

    public static IServiceCollection AddRealtime(this IServiceCollection services)
    {
        services.AddSignalR()
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // Accept the token from the query string, but only on the hub: everywhere else it would end up
        // in URLs that proxies and browsers keep. Request logs record the path without the query.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure(options =>
            options.Events.OnMessageReceived = context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments(DocumentsHub.Path)
                    && context.Request.Query[AccessTokenParameter] is [{ Length: > 0 } token])
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            });

        return services;
    }

    public static IBusRegistrationConfigurator AddRealtimeConsumers(this IBusRegistrationConfigurator bus)
    {
        bus.AddConsumer<DocumentStatusNotifier, DocumentStatusNotifierDefinition>();

        return bus;
    }
}
