using System.Diagnostics;
using System.Text.Json.Serialization;
using DocumentIntelligence.Api.Endpoints;
using DocumentIntelligence.Api.Networking;
using DocumentIntelligence.Api.OpenApi;
using DocumentIntelligence.Api.RateLimiting;
using DocumentIntelligence.Api.Realtime;
using DocumentIntelligence.Application;
using DocumentIntelligence.Infrastructure;
using DocumentIntelligence.Infrastructure.Persistence;
using DocumentIntelligence.ServiceDefaults;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, bus => bus.AddRealtimeConsumers())
    .AddJwtAuthentication()
    .AddRealtime()
    .AddApiRateLimiting()
    .AddTrustedForwardedHeaders(builder.Configuration);

// Enums travel as names ("Completed"), which the frontend and the OpenAPI document can rely on.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddAuthorization();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
builder.Services.AddOpenApi(options => options
    .AddDocumentTransformer<BearerSecurityTransformer>()
    .AddOperationTransformer<BearerSecurityTransformer>());

var app = builder.Build();

// First, so logging and the per-IP rate limit see the client's address rather than the proxy's.
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// OpenAPI + Scalar UI (/scalar): on in Development, opt-in elsewhere via OpenApi:Enabled.
if (app.Configuration.GetValue("OpenApi:Enabled", app.Environment.IsDevelopment()))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();
app.MapAuthEndpoints();
app.MapDocumentEndpoints();
// Close the connection when its access token expires, so a leaked token or a logout does not keep
// the stream open; the client reconnects with a fresh token.
app.MapHub<DocumentsHub>(DocumentsHub.Path, options => options.CloseOnAuthenticationExpiration = true);

if (app.Configuration.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment()))
{
    await app.Services.MigrateDatabaseAsync();
}

await app.RunAsync();
