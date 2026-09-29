using DocumentIntelligence.Application;
using DocumentIntelligence.Infrastructure;
using DocumentIntelligence.ServiceDefaults;
using DocumentIntelligence.Worker.Consumers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, bus => bus.AddWorkerConsumers());

var host = builder.Build();

await host.RunAsync();
