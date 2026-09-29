using DocumentIntelligence.Domain.Abstractions;

namespace DocumentIntelligence.Domain.Documents;

public readonly record struct FileSize
{
    public const long MaxBytes = 10 * 1024 * 1024;

    public FileSize(long bytes)
    {
        if (bytes is <= 0 or > MaxBytes)
        {
            throw new DomainException($"File size must be between 1 byte and {MaxBytes} bytes.");
        }

        Bytes = bytes;
    }

    public long Bytes { get; }

    public override string ToString() => $"{Bytes} B";
}
