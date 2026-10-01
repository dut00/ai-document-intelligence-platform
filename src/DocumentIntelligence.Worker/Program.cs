using DocumentIntelligence.Application;
using DocumentIntelligence.Infrastructure;
using DocumentIntelligence.ServiceDefaults;
using DocumentIntelligence.Worker.Consumers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var role = builder.Configuration.GetValue(WorkerConsumers.RoleSetting, WorkerRole.All);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, bus => bus.AddWorkerConsumers(role));

if (role == WorkerRole.Main)
{
    builder.Services.AddHostedService<IsolatedQueueDeclarer>();
}

var host = builder.Build();

await host.RunAsync();
