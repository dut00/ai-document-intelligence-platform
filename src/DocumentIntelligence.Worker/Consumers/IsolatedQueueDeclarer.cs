using DocumentIntelligence.Infrastructure;
using RabbitMQ.Client;

namespace DocumentIntelligence.Worker.Consumers;

/// <summary>
/// Declares the isolated endpoint's queue and binds it to its exchange when a <see cref="WorkerRole.Main"/>
/// Worker starts. Hand-overs go to the exchange, and RabbitMQ silently drops a message an exchange cannot
/// route: without this, a document redelivered before any Isolated Worker had ever started would be lost
/// and stay pending forever. The arguments match what MassTransit declares for the endpoint.
/// </summary>
public sealed partial class IsolatedQueueDeclarer(IConfiguration configuration, ILogger<IsolatedQueueDeclarer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString(DependencyInjection.MessageBrokerConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{DependencyInjection.MessageBrokerConnectionName}' is not configured.");
        var name = IsolatedDocumentConsumerDefinition.QueueName;

        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(name, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(
            name,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(name, name, routingKey: string.Empty, cancellationToken: cancellationToken);

        LogDeclared(logger, name);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Declared the isolated processing queue {QueueName}")]
    private static partial void LogDeclared(ILogger logger, string queueName);
}
