using Nemetschek.Export.Core.Domain.Exports;

namespace Nemetschek.Export.Core.Clients.Interfaces;

/// <summary>
/// The Archive, as the Export service is allowed to reach it: over HTTP, and nothing else.
/// </summary>
public interface IArchiveClient
{
    /// <summary>
    /// Reads every document stored in a window of calendar dates, oldest first, a page at a time.
    /// The pages are the Archive's own: the caller sees one sequence.
    /// </summary>
    IAsyncEnumerable<ArchiveDocument> ReadDocumentsAsync(
        string cabinetName,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    /// <summary>Opens one part's content. The caller disposes the stream.</summary>
    Task<Stream> OpenPartAsync(
        string cabinetName,
        Guid documentId,
        Guid partId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes one text index value onto several documents in a single request. The Archive refuses
    /// the lot if the field cannot take the value, or if it does not hold one of the documents.
    /// </summary>
    Task SetTextIndexValueAsync(
        string cabinetName,
        IReadOnlyCollection<Guid> documentIds,
        string fieldName,
        string value,
        CancellationToken cancellationToken);
}
