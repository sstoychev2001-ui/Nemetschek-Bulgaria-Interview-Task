using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nemetschek.Archive.Core.Services.Documents;
using Nemetschek.Archive.Core.Services.Interfaces.Documents;
using Nemetschek.Archive.Infrastructure.Health;

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

        var root = CabinetsServiceCollectionExtensions.ResolvePath(storagePath, basePath);

        // Here rather than inside the registration below, so a root the service cannot use stops
        // startup instead of failing the first request that needs it.
        DocumentsStoreService.Prepare(root);

        // Registered under both: the port is what the rules depend on, and the concrete type is
        // what the readiness check needs in order to probe the volume. One instance either way —
        // the store keeps no state, but it does hold a logger, so it is built from the container.
        services.AddSingleton(provider => new DocumentsStoreService(
            root,
            TimeProvider.System,
            provider.GetRequiredService<ILogger<DocumentsStoreService>>()));
        services.AddSingleton<IDocumentStore>(
            provider => provider.GetRequiredService<DocumentsStoreService>());
        services.AddScoped<DocumentsService>();
        services.AddHealthChecks().AddCheck<DocumentStorageHealthCheck>("storage", tags: ["ready"]);

        return services;
    }
}
