using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.Domain.Documents;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(DocumentId id, CancellationToken cancellationToken = default);

    Task<int> CountByOwnerAsync(UserId ownerId, CancellationToken cancellationToken = default);

    void Add(Document document);

    void Remove(Document document);
}
