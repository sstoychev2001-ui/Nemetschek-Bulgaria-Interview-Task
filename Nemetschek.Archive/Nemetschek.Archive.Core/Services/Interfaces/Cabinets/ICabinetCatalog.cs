using Nemetschek.Archive.Core.Domain.Cabinets;

namespace Nemetschek.Archive.Core.Services.Interfaces.Cabinets;

/// <summary>
/// Read-only access to the file cabinets. They are loaded from cabinets.json by Infrastructure and
/// never change at runtime, so there is no way here to create, change or delete one.
/// </summary>
public interface ICabinetCatalog
{
    /// <summary>Every file cabinet, in the order declared in cabinets.json.</summary>
    IReadOnlyList<Cabinet> All { get; }

    /// <summary>
    /// Finds a cabinet by technical name, case-insensitively. Returns <c>null</c> if there is none.
    /// </summary>
    Cabinet? Find(string cabinetName);
}
