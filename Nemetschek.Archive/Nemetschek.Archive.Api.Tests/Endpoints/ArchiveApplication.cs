using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Nemetschek.Archive.Api.Controllers;
using Nemetschek.Archive.Api.Tests.Architecture;

namespace Nemetschek.Archive.Api.Tests.Endpoints;

/// <summary>
/// The real service, in process, on a throwaway storage directory.
/// </summary>
// This boots Program.cs itself, so the endpoint tests cover the wiring a unit test cannot: the
// configuration both paths are required from, the middleware order, the status-code middleware and
// the controllers. It needs no container and no network — the only inputs are the repository's own
// fixtures/cabinets.json and a temp directory — so `dotnet test` stays offline as the brief requires.
internal sealed class ArchiveApplication : WebApplicationFactory<CabinetsController>
{
    private readonly string storagePath =
        Path.Combine(Path.GetTempPath(), $"archive-endpoints-{Guid.NewGuid():N}");

    /// <summary>Most documents one page may hold, as `Archive:MaxPageSize` would set it.</summary>
    public int MaxPageSize { get; init; } = 50;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The same settings compose supplies as Archive__* variables. Neither path is defaulted, so
        // a run without them would fail startup — which is itself asserted below.
        builder.UseSetting("Archive:CabinetsFile", RepositoryLayout.Path("fixtures", "cabinets.json"));
        builder.UseSetting("Archive:StoragePath", storagePath);
        builder.UseSetting("Archive:MaxPageSize", MaxPageSize.ToString());
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
