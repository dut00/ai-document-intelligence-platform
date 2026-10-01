using DocumentIntelligence.Application;
using DocumentIntelligence.Infrastructure;
using DocumentIntelligence.ServiceDefaults;
using DocumentIntelligence.Worker.Consumers;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var role = builder.Configuration.GetValue(WorkerConsumers.RoleSetting, WorkerRole.All);

// On shutdown the bus waits for the documents in hand (an AI call may take up to 2 minutes): a message
// returned unfinished would count as an interrupted delivery, as if this document had crashed the Worker.
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = WorkerConsumers.ShutdownTimeout);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, bus => bus.AddWorkerConsumers(role));

if (role == WorkerRole.Main)
{
    builder.Services.AddHostedService<IsolatedQueueDeclarer>();
}

var host = builder.Build();

await host.RunAsync();
