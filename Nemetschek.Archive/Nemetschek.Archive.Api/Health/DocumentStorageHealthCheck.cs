using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nemetschek.Archive.Infrastructure.Documents;

namespace Nemetschek.Archive.Api.Health;

/// <summary>Reports whether the Archive can write to its storage.</summary>
internal sealed class DocumentStorageHealthCheck(FileSystemDocumentStore store) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(store.CanWrite()
            ? HealthCheckResult.Healthy($"Storage at {store.Root} is writable.")
            : HealthCheckResult.Unhealthy($"Storage at {store.Root} is not writable."));
}
