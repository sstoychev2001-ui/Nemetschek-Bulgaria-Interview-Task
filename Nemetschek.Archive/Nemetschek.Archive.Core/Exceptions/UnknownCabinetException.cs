using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Exceptions;

/// <summary>
/// Thrown when a request names a file cabinet that does not exist.
/// </summary>
public sealed class UnknownCabinetException(string cabinetName)
    : InvalidOperationException($"There is no file cabinet named '{cabinetName}'.")
{
    /// <summary>The name the caller asked for.</summary>
    public string CabinetName { get; } = cabinetName;
}
