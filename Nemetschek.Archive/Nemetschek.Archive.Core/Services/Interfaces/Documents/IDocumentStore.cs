using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Services.Interfaces.Documents;

/// <summary>
/// Where documents and their part content are kept.
/// </summary>
public interface IDocumentStore
{
    /// <summary>
    /// Stores one document with its parts and returns it with the system fields set. Any
    /// <see cref="Document.Id"/>, <see cref="Document.StoredAt"/> or
    /// <see cref="Document.ModifiedAt"/> on the way in is ignored and replaced: those are the
    /// Archive's to set. Every part must carry <see cref="Part.OpenContent"/>.
    /// </summary>
    Task<Document> AddAsync(Document document, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one page of a cabinet's documents, oldest first, within a window of time.
    /// </summary>
    /// <param name="cabinetName">The technical name of the file cabinet.</param>
    /// <param name="storedAt">The window the documents must have been stored in.</param>
    /// <param name="skip">How many documents to pass over.</param>
    /// <param name="take">How many documents the page holds at most.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<DocumentPage> ListAsync(
        string cabinetName,
        StoredRange storedAt,
        int skip,
        int take,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads one document by id, or <c>null</c> when the cabinet does not hold it.
    /// </summary>
    Task<Document?> FindAsync(string cabinetName, Guid documentId, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a part's content for reading. The caller disposes the stream. Returns <c>null</c> when
    /// the document or the part is not there.
    /// </summary>
    Task<Stream?> OpenPartAsync(
        string cabinetName,
        Guid documentId,
        Guid partId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the named index values of a stored document and returns it as it now stands. Values
    /// that are not named are left alone, and <see cref="Document.ModifiedAt"/> is the Archive's to
    /// set. Returns <c>null</c> when the cabinet does not hold the document.
    /// </summary>
    Task<Document?> SetIndexValuesAsync(
        string cabinetName,
        Guid documentId,
        IReadOnlyDictionary<string, IndexValue> values,
        CancellationToken cancellationToken);
}
