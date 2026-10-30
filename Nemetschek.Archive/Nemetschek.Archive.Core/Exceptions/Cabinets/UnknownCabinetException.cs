namespace Nemetschek.Archive.Core.Exceptions.Cabinets;

/// <summary>
/// Thrown when a request names a file cabinet that does not exist.
/// </summary>
public sealed class UnknownCabinetException(string cabinetName)
    : InvalidOperationException($"There is no file cabinet named '{cabinetName}'.")
{
    /// <summary>The name the caller asked for.</summary>
    public string CabinetName { get; } = cabinetName;
}
