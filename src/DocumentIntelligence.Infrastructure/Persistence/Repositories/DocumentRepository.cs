using DocumentIntelligence.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace DocumentIntelligence.Infrastructure.Persistence.Repositories;

internal sealed class DocumentRepository(ApplicationDbContext dbContext) : IDocumentRepository
{
    public Task<Document?> GetByIdAsync(DocumentId id, CancellationToken cancellationToken = default) =>
        dbContext.Documents.SingleOrDefaultAsync(document => document.Id == id, cancellationToken);

    public void Add(Document document) => dbContext.Documents.Add(document);

    public void Remove(Document document) => dbContext.Documents.Remove(document);
}
