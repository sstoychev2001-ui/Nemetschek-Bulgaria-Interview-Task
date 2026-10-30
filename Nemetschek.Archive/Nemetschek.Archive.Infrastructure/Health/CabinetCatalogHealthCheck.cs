using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nemetschek.Archive.Core.Services.Interfaces.Cabinets;

namespace Nemetschek.Archive.Infrastructure.Health;

/// <summary>Reports whether the file cabinet definitions are loaded.</summary>
internal sealed class CabinetCatalogHealthCheck(ICabinetCatalog catalog) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(catalog.All.Count > 0
            ? HealthCheckResult.Healthy($"{catalog.All.Count} file cabinets loaded.")
            : HealthCheckResult.Unhealthy("No file cabinets are loaded."));
}
