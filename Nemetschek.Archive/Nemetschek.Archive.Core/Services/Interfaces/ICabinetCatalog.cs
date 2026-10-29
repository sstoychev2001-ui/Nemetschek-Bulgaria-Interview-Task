using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Services.Interfaces;

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
