using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace DocumentIntelligence.ServiceDefaults;

/// <summary>
/// Host setup shared by the API and the Worker: structured logging, OpenTelemetry and health checks.
/// </summary>
public static class ServiceDefaultsExtensions
{
    public const string LiveTag = "live";

    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    // Telemetry of this solution (DocumentIntelligence.Api, DocumentIntelligence.Processing, ...) and of the
    // libraries that emit it natively: no instrumentation package is needed for these.
    private static readonly string[] _telemetrySources = ["DocumentIntelligence.*", "MassTransit", "Npgsql"];

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.AddLogging();
        builder.AddOpenTelemetry();
        builder.AddDefaultHealthChecks();

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Liveness only checks that the process responds; readiness runs every registered check.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(LiveTag),
            ResponseWriter = WriteHealthReportAsync,
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthReportAsync,
        });

        return app;
    }

    private static void AddLogging(this IHostApplicationBuilder builder)
    {
        // Serilog owns the console output (JSON with trace/span ids) and forwards events
        // to the remaining providers, i.e. the OpenTelemetry logger added below.
        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(
            (services, configuration) => configuration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
                .WriteTo.Console(new RenderedCompactJsonFormatter()),
            writeToProviders: true);
    }

    private static void AddOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var openTelemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
            .WithMetrics(metrics => metrics
                .AddMeter(_telemetrySources)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddSource(_telemetrySources)
                .AddAspNetCoreInstrumentation(options => options.Filter = IsNotHealthCheck)
                .AddHttpClientInstrumentation());

        // Export only when a collector is configured, e.g. the Aspire Dashboard.
        if (!string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]))
        {
            openTelemetry.UseOtlpExporter();
        }
    }

    private static void AddDefaultHealthChecks(this IHostApplicationBuilder builder) =>
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);

    // One entry per check, e.g. to see which dependency makes the service not ready. Exceptions are left out.
    private static Task WriteHealthReportAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                duration = entry.Value.Duration,
                description = entry.Value.Description,
            }),
        });

    private static bool IsNotHealthCheck(HttpContext context) =>
        !context.Request.Path.StartsWithSegments("/health");
}
