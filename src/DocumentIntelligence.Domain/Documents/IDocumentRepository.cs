namespace DocumentIntelligence.Domain.Documents;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(DocumentId id, CancellationToken cancellationToken = default);

    void Add(Document document);

    void Remove(Document document);
}
