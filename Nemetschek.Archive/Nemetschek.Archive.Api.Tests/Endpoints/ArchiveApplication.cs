using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Nemetschek.Archive.Api.Controllers;
using Nemetschek.Archive.Api.Tests.Architecture;

namespace Nemetschek.Archive.Api.Tests.Endpoints;

/// <summary>
/// The real service, in process, on a throwaway storage directory.
/// </summary>
internal sealed class ArchiveApplication : WebApplicationFactory<CabinetsController>
{
    private readonly string storagePath =
        Path.Combine(Path.GetTempPath(), $"archive-endpoints-{Guid.NewGuid():N}");

    /// <summary>Most documents one page may hold, as `Archive:MaxPageSize` would set it.</summary>
    public int MaxPageSize { get; init; } = 50;

    /// <summary>The origins the client may be served from, as `Archive:AllowedOrigins` would set them.</summary>
    public string AllowedOrigins { get; init; } = "";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Archive:CabinetsFile", RepositoryLayout.Path("fixtures", "cabinets.json"));
        builder.UseSetting("Archive:StoragePath", storagePath);
        builder.UseSetting("Archive:MaxPageSize", MaxPageSize.ToString());
        builder.UseSetting("Archive:AllowedOrigins", AllowedOrigins);
        builder.UseEnvironment("Production");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(storagePath))
        {
            Directory.Delete(storagePath, recursive: true);
        }
    }
}
