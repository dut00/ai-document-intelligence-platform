using DocumentIntelligence.Infrastructure;
using DocumentIntelligence.ServiceDefaults;
using DocumentIntelligence.Worker.Consumers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddInfrastructure(
    builder.Configuration,
    bus => bus.AddConsumer<DocumentDeletedConsumer>());

var host = builder.Build();

await host.RunAsync();
