using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Services.Interfaces.Cabinets;

namespace Nemetschek.Archive.Core.Services.Cabinets;

/// <summary>
/// An <see cref="ICabinetCatalog"/> over a fixed set of cabinets held in memory.
/// </summary>
public sealed class CabinetCatalog(IReadOnlyList<Cabinet> cabinets) : ICabinetCatalog
{
    private readonly Dictionary<string, Cabinet> cabinetsByName =
        cabinets.ToDictionary(cabinet => cabinet.Name, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyList<Cabinet> All { get; } = cabinets;

    /// <inheritdoc />
    public Cabinet? Find(string cabinetName) => cabinetsByName.GetValueOrDefault(cabinetName);
}
