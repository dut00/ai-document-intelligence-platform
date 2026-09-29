using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.Domain.Documents;

/// <summary>
/// Location of a document's content in object storage.
/// </summary>
public sealed record StorageKey
{
    public StorageKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Storage key must not be empty.");
        }

        Value = value;
    }

    public string Value { get; }

    public static StorageKey For(UserId ownerId, DocumentId documentId) =>
        new($"users/{ownerId}/{documentId}");

    public override string ToString() => Value;
}
