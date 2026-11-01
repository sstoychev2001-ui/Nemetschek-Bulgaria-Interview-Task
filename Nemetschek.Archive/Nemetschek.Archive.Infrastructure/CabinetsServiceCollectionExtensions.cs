using Microsoft.Extensions.DependencyInjection;
using Nemetschek.Archive.Infrastructure.Cabinets;
using Nemetschek.Archive.Core.Services.Interfaces.Cabinets;
using Nemetschek.Archive.Core.Services.Cabinets;

namespace Nemetschek.Archive.Infrastructure;

/// <summary>
/// Registers the file cabinets with the dependency injection container.
/// </summary>
public static class CabinetsServiceCollectionExtensions
{
    /// <summary>
    /// Loads the file cabinets from cabinets.json and registers them as the <see cref="ICabinetCatalog"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="cabinetsFilePath">
    /// Path to cabinets.json. A relative path is resolved against <paramref name="basePath"/>.
    /// </param>
    /// <param name="basePath">
    /// Directory that relative paths are resolved against — normally the host's content root.
    /// </param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="InvalidDataException">The file does not describe usable cabinets.</exception>
    public static IServiceCollection AddCabinetCatalog(
        this IServiceCollection services,
        string cabinetsFilePath,
        string basePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cabinetsFilePath);

        var resolved = ResolvePath(cabinetsFilePath, basePath);
        var catalog = new CabinetCatalog(CabinetsFileLoader.Load(resolved));

        services.AddSingleton<ICabinetCatalog>(catalog);

        return services;
    }

    /// <summary>
    /// Turns a possibly-relative configured path into an absolute one.
    /// </summary>
    public static string ResolvePath(string cabinetsFilePath, string basePath) =>
        Path.IsPathRooted(cabinetsFilePath)
            ? Path.GetFullPath(cabinetsFilePath)
            : Path.GetFullPath(Path.Combine(basePath, cabinetsFilePath));
}
