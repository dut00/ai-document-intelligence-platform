namespace DocumentIntelligence.Worker.Consumers;

/// <summary>
/// A document whose <c>DocumentUploaded</c> delivery was interrupted (the Worker died while holding it),
/// to be processed again on its own; see <see cref="IsolatedDocumentConsumer"/>. Internal to the Worker.
/// </summary>
public sealed record ProcessDocumentInIsolation(Guid DocumentId);
