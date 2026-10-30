using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nemetschek.Archive.Core.Services.Documents;

namespace Nemetschek.Archive.Infrastructure.Health;

/// <summary>Reports whether the Archive can write to its storage.</summary>
internal sealed class DocumentStorageHealthCheck(DocumentsStoreService storeService) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(storeService.CanWrite()
            ? HealthCheckResult.Healthy($"Storage at {storeService.Root} is writable.")
            : HealthCheckResult.Unhealthy($"Storage at {storeService.Root} is not writable."));
}
