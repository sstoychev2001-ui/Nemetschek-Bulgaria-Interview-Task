using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Services.Interfaces;

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
