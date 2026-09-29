namespace DocumentIntelligence.Application.Abstractions.Analysis;

/// <summary>
/// A permanent processing failure: retrying cannot help, so the document is marked as failed
/// right away. <see cref="Exception.Message"/> is shown to the document's owner.
/// </summary>
public sealed class UnprocessableDocumentException(string message, Exception? innerException = null)
    : Exception(message, innerException);
