namespace DocumentIntelligence.Contracts.Documents;

/// <summary>
/// A document was stored and is waiting to be processed by the Worker.
/// </summary>
public sealed record DocumentUploaded(Guid DocumentId, Guid OwnerId);
