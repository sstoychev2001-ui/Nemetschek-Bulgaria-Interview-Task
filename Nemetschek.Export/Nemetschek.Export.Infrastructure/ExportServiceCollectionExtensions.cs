using Microsoft.Extensions.DependencyInjection;
using Nemetschek.Export.Core.Services.Exports;
using Nemetschek.Export.Core.Services.Interfaces;
using Nemetschek.Export.Infrastructure.Archive;

namespace Nemetschek.Export.Infrastructure;

/// <summary>
/// Registers the Archive client and the export rules with the dependency injection container.
/// </summary>
public static class ExportServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything an export needs, reaching the Archive at
    /// <paramref name="archiveBaseAddress"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="archiveBaseAddress">
    /// The Archive's base address, such as <c>http://archive-api:8080</c>. Required: there is
    /// nothing to export without it, and a default would hide a missing setting until the first
    /// request.
    /// </param>
    /// <exception cref="ArgumentException">The address is absent or not absolute.</exception>
    public static IServiceCollection AddArchiveClient(
        this IServiceCollection services,
        string archiveBaseAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveBaseAddress);

        if (!Uri.TryCreate(archiveBaseAddress, UriKind.Absolute, out var baseAddress))
        {
            throw new ArgumentException(
                $"'{archiveBaseAddress}' is not an absolute URL. Set 'Export:ArchiveBaseAddress' " +
                "to something like http://archive-api:8080.",
                nameof(archiveBaseAddress));
        }

        services.AddHttpClient<IArchiveClient, ArchiveHttpClient>(client =>
        {
            client.BaseAddress = Ends(baseAddress);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        services.AddScoped<ExportService>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>The address with the trailing slash a relative request needs.</summary>
    private static Uri Ends(Uri baseAddress) =>
        baseAddress.AbsoluteUri.EndsWith('/') ? baseAddress : new Uri($"{baseAddress.AbsoluteUri}/");
}
