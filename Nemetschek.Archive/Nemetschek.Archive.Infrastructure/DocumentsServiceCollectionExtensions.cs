using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nemetschek.Archive.Core.Services.Documents;
using Nemetschek.Archive.Core.Services.Interfaces.Documents;
using Nemetschek.Archive.Core.Utils.Documents;

namespace Nemetschek.Archive.Infrastructure;

/// <summary>
/// Registers the document store with the dependency injection container.
/// </summary>
public static class DocumentsServiceCollectionExtensions
{
    /// <summary>
    /// Prepares the Archive's storage directory and registers the store that writes to it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="storagePath">
    /// Directory the Archive keeps its documents in. A relative path is resolved against
    /// <paramref name="basePath"/>. It must survive a container restart — see README.md.
    /// </param>
    /// <param name="basePath">
    /// Directory that relative paths are resolved against — normally the host's content root.
    /// </param>
    /// <exception cref="IOException">The storage directory cannot be created or cleared.</exception>
    /// <exception cref="UnauthorizedAccessException">The storage directory is not writable.</exception>
    public static IServiceCollection AddDocumentsStore(
        this IServiceCollection services,
        string storagePath,
        string basePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        var resolvedStoragePath =
            CabinetsServiceCollectionExtensions.ResolvePath(storagePath, basePath);

        PrepareStorage(resolvedStoragePath);

        services.AddSingleton<IDocumentStore>(provider => new DocumentsStoreService(
            resolvedStoragePath,
            TimeProvider.System,
            provider.GetRequiredService<ILogger<DocumentsStoreService>>()));
        services.AddScoped<DocumentsService>();

        return services;
    }

    /// <summary>
    /// Creates the storage directories and clears whatever an upload left in flight.
    /// </summary>
    /// <exception cref="IOException">The storage directory cannot be created or cleared.</exception>
    private static void PrepareStorage(string resolvedStoragePath)
    {
        Directory.CreateDirectory(Path.Combine(resolvedStoragePath, DocumentStorageUtils.CabinetsFolder));

        var staging = DocumentStorageUtils.Staging(resolvedStoragePath);
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        Directory.CreateDirectory(staging);
    }
}
