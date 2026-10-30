using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Nemetschek.Export.Infrastructure.Health;

/// <summary>Reports whether the Archive answers, which is the only thing this service needs.</summary>
internal sealed class ArchiveReachableHealthCheck(HttpClient http) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.GetAsync("health", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"The Archive at {http.BaseAddress} answered.")
                : HealthCheckResult.Unhealthy(
                    $"The Archive at {http.BaseAddress} answered {(int)response.StatusCode}.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                $"The Archive at {http.BaseAddress} is unreachable.", exception);
        }
    }
}
