using System.Diagnostics;
using DocumentIntelligence.Api.Endpoints;
using DocumentIntelligence.Api.OpenApi;
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
    .AddInfrastructure(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
builder.Services.AddOpenApi(options => options
    .AddDocumentTransformer<BearerSecurityTransformer>()
    .AddOperationTransformer<BearerSecurityTransformer>());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

// OpenAPI + Scalar UI (/scalar): on in Development, opt-in elsewhere via OpenApi:Enabled.
if (app.Configuration.GetValue("OpenApi:Enabled", app.Environment.IsDevelopment()))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();
app.MapAuthEndpoints();

if (app.Configuration.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment()))
{
    await app.Services.MigrateDatabaseAsync();
}

await app.RunAsync();
