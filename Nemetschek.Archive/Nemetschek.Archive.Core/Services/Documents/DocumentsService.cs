using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Exceptions.Cabinets;
using Nemetschek.Archive.Core.Exceptions.Documents;
using Nemetschek.Archive.Core.Services.Interfaces.Cabinets;
using Nemetschek.Archive.Core.Services.Interfaces.Documents;
using Nemetschek.Archive.Core.Validation.IndexFields;

namespace Nemetschek.Archive.Core.Services.Documents;

/// <summary>
/// Service that interacts with stored documents
/// </summary>
public sealed class DocumentsService(ICabinetCatalog cabinets, IDocumentStore documents)
{
    /// <summary>Reads one page of a cabinet's documents, within a window of time.</summary>
    /// <param name="cabinetName">The technical name of the file cabinet.</param>
    /// <param name="storedAt">The window the documents must have been stored in.</param>
    /// <param name="skip">How many documents to pass over.</param>
    /// <param name="take">How many documents the page holds at most.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="UnknownCabinetException">There is no such file cabinet.</exception>
    public async Task<DocumentPage> ListAsync(
        string cabinetName,
        StoredRange storedAt,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var cabinet = cabinets.Find(cabinetName) ?? throw new UnknownCabinetException(cabinetName);

        return await documents.ListAsync(
            cabinet.Name, storedAt, skip, take, cancellationToken);
    }

    /// <summary>Opens the content of one part of one document.</summary>
    /// <exception cref="UnknownCabinetException">There is no such file cabinet.</exception>
    /// <exception cref="DocumentNotFoundException">There is no such document, or no such part.</exception>
    public async Task<Stream> OpenPartAsync(
        string cabinetName,
        Guid documentId,
        Guid partId,
        CancellationToken cancellationToken) =>
        await documents.OpenPartAsync(
            Found(cabinetName).Name, documentId, partId, cancellationToken);

    /// <summary>
    /// Sets the same index values on several stored documents and returns how many were changed.
    /// Every value is checked against the cabinet's field rules first, and every document is
    /// resolved before any of them is written, so a value that breaks a rule or an id the cabinet
    /// does not hold changes nothing.
    /// </summary>
    /// <exception cref="UnknownCabinetException">There is no such file cabinet.</exception>
    /// <exception cref="DocumentNotFoundException">The cabinet does not hold one of the documents.</exception>
    /// <exception cref="IndexValueRefusedException">A value breaks an index field rule.</exception>
    public async Task<int> SetIndexValuesAsync(
        string cabinetName,
        IReadOnlyCollection<Guid> documentIds,
        IReadOnlyDictionary<string, IndexValue> values,
        CancellationToken cancellationToken)
    {
        var cabinet = Found(cabinetName);

        if (IndexFieldRules.ValidateWrites(cabinet, values) is { Count: > 0 } refusals)
        {
            throw new IndexValueRefusedException(refusals);
        }

        foreach (var documentId in documentIds)
        {
            await documents.FindAsync(cabinet.Name, documentId, cancellationToken);
        }

        foreach (var documentId in documentIds)
        {
            await documents.SetIndexValuesAsync(cabinet.Name, documentId, values, cancellationToken);
        }

        return documentIds.Count;
    }

    /// <summary>The cabinet a request names.</summary>
    /// <exception cref="UnknownCabinetException">There is no such file cabinet.</exception>
    private Cabinet Found(string cabinetName) =>
        cabinets.Find(cabinetName) ?? throw new UnknownCabinetException(cabinetName);
}
