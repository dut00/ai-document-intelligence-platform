using DocumentIntelligence.Application.Abstractions.Results;

namespace DocumentIntelligence.Application.Documents;

public static class DocumentErrors
{
    /// <summary>
    /// Also returned for another user's document, so its existence is not revealed.
    /// </summary>
    public static readonly Error NotFound =
        Error.NotFound("Document.NotFound", "The document was not found.");

    public static readonly Error ContentNotFound =
        Error.NotFound("Document.ContentNotFound", "The document content is no longer available.");
}
