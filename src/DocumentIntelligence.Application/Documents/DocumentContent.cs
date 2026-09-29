namespace DocumentIntelligence.Application.Documents;

/// <summary>
/// A stored file ready to stream to the client. Whoever sends it disposes <see cref="Content"/>.
/// </summary>
public sealed record DocumentContent(Stream Content, string FileName, string ContentType);
