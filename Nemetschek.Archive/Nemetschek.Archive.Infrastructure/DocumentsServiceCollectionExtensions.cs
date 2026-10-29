using Microsoft.Extensions.DependencyInjection;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Ports;
using Nemetschek.Archive.Infrastructure.Documents;

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
    // The directory is prepared here, during registration, so a storage path that cannot be used
    // stops the service with a clear error instead of failing every upload later. Same reasoning
    // as AddCabinetCatalog.
    public static IServiceCollection AddFileSystemDocumentStore(
        this IServiceCollection services,
        string storagePath,
        string basePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        var store = new FileSystemDocumentStore(
            CabinetsServiceCollectionExtensions.ResolvePath(storagePath, basePath),
            TimeProvider.System);

        store.Prepare();

        // Registered under both: the port is what the rules depend on, and the concrete type is
        // what the readiness check needs in order to probe the volume.
        services.AddSingleton(store);
        services.AddSingleton<IDocumentStore>(store);
        services.AddScoped<DocumentQueryService>();

        return services;
    }
}
