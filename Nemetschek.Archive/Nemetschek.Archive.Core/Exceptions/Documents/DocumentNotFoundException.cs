namespace Nemetschek.Archive.Core.Exceptions.Documents;

/// <summary>
/// Thrown when a request names a document, or a part of one, that the Archive does not hold.
/// </summary>
public sealed class DocumentNotFoundException(string cabinetName, Guid documentId, Guid? partId = null)
    : InvalidOperationException(partId is { } part
        ? $"Document '{documentId}' in file cabinet '{cabinetName}' has no part '{part}'."
        : $"File cabinet '{cabinetName}' holds no document '{documentId}'.")
{
    /// <summary>The document the caller asked for.</summary>
    public Guid DocumentId { get; } = documentId;
}
