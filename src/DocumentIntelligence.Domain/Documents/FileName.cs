using DocumentIntelligence.Domain.Abstractions;

namespace DocumentIntelligence.Domain.Documents;

/// <summary>
/// Original name of an uploaded file, without any directory part.
/// </summary>
public sealed record FileName
{
    public const int MaxLength = 255;

    public FileName(string value)
    {
        // Browsers may send a full client path; keep only the last segment of either separator style.
        var name = value?.Split('/', '\\')[^1].Trim();

        if (string.IsNullOrEmpty(name))
        {
            throw new DomainException("File name must not be empty.");
        }

        if (name.Length > MaxLength)
        {
            throw new DomainException($"File name must not exceed {MaxLength} characters.");
        }

        Value = name;
    }

    public string Value { get; }

    public string Extension => Path.GetExtension(Value).ToLowerInvariant();

    public override string ToString() => Value;
}
