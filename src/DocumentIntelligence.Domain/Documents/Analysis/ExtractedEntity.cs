using DocumentIntelligence.Domain.Abstractions;

namespace DocumentIntelligence.Domain.Documents.Analysis;

public enum EntityType
{
    Person,
    Organization,
    Location,
    Other,
}

/// <summary>
/// A named party, place or other entity mentioned in a document.
/// </summary>
public sealed record ExtractedEntity
{
    public ExtractedEntity(EntityType type, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Entity name must not be empty.");
        }

        Type = type;
        Name = name.Trim();
    }

    public EntityType Type { get; }

    public string Name { get; }
}
