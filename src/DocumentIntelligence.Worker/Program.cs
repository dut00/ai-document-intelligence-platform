using DocumentIntelligence.Application;
using DocumentIntelligence.Infrastructure;
using DocumentIntelligence.ServiceDefaults;
using DocumentIntelligence.Worker.Consumers;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

var role = builder.Configuration.GetValue(WorkerConsumers.RoleSetting, WorkerRole.All);

// On shutdown the documents in hand are allowed to finish: a message returned unfinished would count as an
// interrupted delivery, as if this document had crashed the Worker. MassTransit cancels the consumers'
// tokens after ConsumerStopTimeout, and the host gives it that long and more.
builder.Services.Configure<MassTransitHostOptions>(options =>
{
    options.ConsumerStopTimeout = WorkerConsumers.ConsumerStopTimeout;
    options.StopTimeout = WorkerConsumers.ConsumerStopTimeout + TimeSpan.FromSeconds(15);
});
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
