using DocumentIntelligence.Domain.Documents;

namespace DocumentIntelligence.Application.Abstractions.Data;

/// <summary>
/// Read side of CQRS: untracked queries that project straight to DTOs.
/// </summary>
public interface IReadDbContext
{
    IQueryable<Document> Documents { get; }
}
