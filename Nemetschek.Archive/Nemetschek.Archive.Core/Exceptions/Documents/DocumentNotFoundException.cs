namespace Nemetschek.Archive.Core.Exceptions.Documents;

/// <summary>
/// Thrown when a request names a document, or a part of one, that the Archive does not hold.
/// </summary>
public sealed class DocumentNotFoundException : InvalidOperationException
{
    /// <summary>A document the file cabinet does not hold.</summary>
    public DocumentNotFoundException(string cabinetName, Guid documentId)
        : base($"File cabinet '{cabinetName}' holds no document '{documentId}'.") =>
        DocumentId = documentId;

    /// <summary>A part the document does not have.</summary>
    public DocumentNotFoundException(string cabinetName, Guid documentId, Guid partId)
        : base($"Document '{documentId}' in file cabinet '{cabinetName}' has no part '{partId}'.") =>
        DocumentId = documentId;

    /// <summary>The document the caller asked for.</summary>
    public Guid DocumentId { get; }
}
