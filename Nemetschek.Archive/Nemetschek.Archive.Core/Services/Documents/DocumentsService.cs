using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Exceptions.Cabinets;
using Nemetschek.Archive.Core.Services.Interfaces.Cabinets;
using Nemetschek.Archive.Core.Services.Interfaces.Documents;

namespace Nemetschek.Archive.Core.Services.Documents;

/// <summary>
/// Reads documents out of a file cabinet.
/// </summary>
public sealed class DocumentsService(ICabinetCatalog cabinets, IDocumentStore documents)
{
    /// <summary>Reads one page of a cabinet's documents.</summary>
    /// <param name="cabinetName">The technical name of the file cabinet.</param>
    /// <param name="skip">How many documents to pass over.</param>
    /// <param name="take">How many documents the page holds at most.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="UnknownCabinetException">There is no such file cabinet.</exception>
    public async Task<DocumentPage> ListAsync(
        string cabinetName,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        if (cabinets.Find(cabinetName) is not { } cabinet)
        {
            throw new UnknownCabinetException(cabinetName);
        }

        return await documents.ListAsync(cabinet.Name, skip, take, cancellationToken);
    }
}
