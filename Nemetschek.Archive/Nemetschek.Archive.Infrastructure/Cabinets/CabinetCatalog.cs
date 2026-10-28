using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Ports;

namespace Nemetschek.Archive.Infrastructure.Cabinets;

/// <summary>
/// An <see cref="ICabinetCatalog"/> over a fixed set of cabinets held in memory.
/// </summary>
// Read once at startup: the cabinets are fixed, so there is nothing to invalidate and no reason to
// touch the disk per request.
public sealed class CabinetCatalog(IReadOnlyList<Cabinet> cabinets) : ICabinetCatalog
{
    private readonly Dictionary<string, Cabinet> _cabinetsByName =
        cabinets.ToDictionary(cabinet => cabinet.Name, StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyList<Cabinet> All { get; } = cabinets;

    /// <summary>Loads the catalog from a cabinets.json file.</summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidDataException">The file does not describe usable cabinets.</exception>
    public static CabinetCatalog FromFile(string filePath) => new(CabinetsFileLoader.Load(filePath));

    /// <inheritdoc />
    public Cabinet? Find(string cabinetName) => _cabinetsByName.GetValueOrDefault(cabinetName);
}
