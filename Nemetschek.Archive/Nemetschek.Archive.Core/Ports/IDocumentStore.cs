using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Core.Ports;

/// <summary>
/// Where documents and their part content are kept.
/// </summary>
public interface IDocumentStore
{
    /// <summary>
    /// Stores one document with its parts and returns it with the system fields set.
    /// </summary>
    // One document at a time, because the upload rules commit per document: when document 7 fails,
    // documents 1-6 stay stored and the report says so.
    Task<Document> AddAsync(DocumentDraft draft, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one page of a cabinet's documents, oldest first.
    /// </summary>
    /// <param name="cabinetName">The technical name of the file cabinet.</param>
    /// <param name="skip">How many documents to pass over.</param>
    /// <param name="take">How many documents the page holds at most.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    // Paged rather than "give me everything", because the Export service reads the Archive page by
    // page and a cabinet is not bounded.
    Task<DocumentPage> ListAsync(
        string cabinetName,
        int skip,
        int take,
        CancellationToken cancellationToken);
}
