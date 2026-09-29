namespace DocumentIntelligence.Domain.Documents;

public readonly record struct DocumentId(Guid Value)
{
    public static DocumentId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
