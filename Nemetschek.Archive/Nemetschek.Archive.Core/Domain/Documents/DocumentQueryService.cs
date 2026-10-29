using Nemetschek.Archive.Core.Ports;

namespace Nemetschek.Archive.Core.Domain.Documents;

/// <summary>
/// Reads documents out of a file cabinet.
/// </summary>
public sealed class DocumentQueryService(ICabinetCatalog cabinets, IDocumentStore documents)
{
    /// <summary>
    /// Reads one page of a cabinet's documents. Returns <c>null</c> if there is no such cabinet.
    /// </summary>
    /// <param name="cabinetName">The technical name of the file cabinet.</param>
    /// <param name="skip">How many documents to pass over.</param>
    /// <param name="take">How many documents the page holds at most.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    // Whether a cabinet exists is a rule, so it is answered here rather than in the endpoint, and
    // the cabinet's declared name is used so the store is keyed consistently however the client
    // spelled it.
    public async Task<DocumentPage?> ListAsync(
        string cabinetName,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        if (cabinets.Find(cabinetName) is not { } cabinet)
        {
            return null;
        }

        return await documents.ListAsync(cabinet.Name, skip, take, cancellationToken);
    }
}
