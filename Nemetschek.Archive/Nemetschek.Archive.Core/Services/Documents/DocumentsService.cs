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
        CancellationToken cancellationToken)
    {
        var cabinet = cabinets.Find(cabinetName) ?? throw new UnknownCabinetException(cabinetName);

        return await documents.OpenPartAsync(cabinet.Name, documentId, partId, cancellationToken)
               ?? throw new DocumentNotFoundException(cabinet.Name, documentId, partId);
    }

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
        var cabinet = cabinets.Find(cabinetName) ?? throw new UnknownCabinetException(cabinetName);

        var refusals = values
            .Select(value => IndexFieldRules.ValidateWrite(cabinet, value.Key, value.Value))
            .OfType<FieldViolation>()
            .Select(violation => violation.Reason)
            .ToList();

        if (refusals.Count > 0)
        {
            throw new IndexValueRefusedException(refusals);
        }

        // Every document is resolved before any of them is written. There is no transaction
        // spanning documents, so the one failure worth ruling out up front is the one a caller
        // causes: an id the cabinet does not hold. It costs a second pass over the directory names,
        // which is the price of not leaving half a batch applied.
        foreach (var documentId in documentIds)
        {
            if (await documents.FindAsync(cabinet.Name, documentId, cancellationToken) is null)
            {
                throw new DocumentNotFoundException(cabinet.Name, documentId);
            }
        }

        var updated = 0;
        foreach (var documentId in documentIds)
        {
            if (await documents.SetIndexValuesAsync(
                    cabinet.Name, documentId, values, cancellationToken) is not null)
            {
                updated++;
            }
        }

        return updated;
    }
}
