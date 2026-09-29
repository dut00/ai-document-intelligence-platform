namespace DocumentIntelligence.Infrastructure.Messaging;

/// <summary>
/// Naming convention for endpoints that only push notifications and never touch the database, e.g.
/// the API's per-instance SignalR notifier. They get neither the transactional inbox/outbox nor
/// retries, and a message that faults is discarded instead of piling up in an error queue: the
/// notification is a hint, and clients refetch the data anyway.
/// </summary>
public static class NotificationEndpoints
{
    public const string NamePrefix = "notify-";

    /// <summary>
    /// A unique, per-process name: each instance must receive every message itself.
    /// </summary>
    public static string InstanceName(string name) => $"{NamePrefix}{name}-{Guid.NewGuid():N}";

    public static bool IsNotificationEndpoint(string endpointName) =>
        endpointName.StartsWith(NamePrefix, StringComparison.Ordinal);
}
