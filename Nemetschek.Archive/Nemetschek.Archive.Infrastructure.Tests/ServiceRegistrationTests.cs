using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Nemetschek.Archive.Core.Services.Documents;
using Nemetschek.Archive.Core.Services.Interfaces.Cabinets;
using Nemetschek.Archive.Core.Services.Interfaces.Documents;

namespace Nemetschek.Archive.Infrastructure.Tests;

/// <summary>
/// What the two <c>Add…</c> extensions wire up, and what they refuse to start without.
/// </summary>
// Startup being fail-fast is a documented property: a storage root or a cabinets file the service
// cannot use stops it rather than failing the first request. These tests are that property.
public sealed class ServiceRegistrationTests : IDisposable
{
    private readonly string root =
        Path.Combine(Path.GetTempPath(), $"archive-registration-{Guid.NewGuid():N}");

    // --- the cabinet catalog -------------------------------------------------------------------

    [Fact]
    public void The_catalog_is_registered_from_the_given_cabinets_file()
    {
        var catalog = Provider(services =>
                services.AddCabinetCatalog(RepositoryFixtures.CabinetsFile, root))
            .GetRequiredService<ICabinetCatalog>();

        Assert.Equal(["Invoices", "Contracts"], catalog.All.Select(cabinet => cabinet.Name));
    }

    [Fact]
    public void The_catalog_is_read_once_and_shared()
    {
        // cabinets.json is fixed, so the catalog is the one thing the Archive may hold in memory.
        var provider = Provider(services =>
            services.AddCabinetCatalog(RepositoryFixtures.CabinetsFile, root));

        Assert.Same(
            provider.GetRequiredService<ICabinetCatalog>(),
            provider.GetRequiredService<ICabinetCatalog>());
    }

    [Fact]
    public void A_cabinets_file_that_is_not_there_stops_startup_and_names_the_file()
    {
        Directory.CreateDirectory(root);
        var missing = Path.Combine(root, "no-such-cabinets.json");

        var failure = Assert.Throws<FileNotFoundException>(() =>
            new ServiceCollection().AddCabinetCatalog(missing, root));

        // The SDK's own exception carries the path, which is why no custom type was invented.
        Assert.Equal(missing, failure.FileName);
    }

    [Fact]
    public void A_cabinets_path_whose_directory_is_not_there_also_stops_startup()
    {
        // A forgotten bind mount looks like this rather than like a missing file.
        Assert.Throws<DirectoryNotFoundException>(() =>
            new ServiceCollection().AddCabinetCatalog(
                Path.Combine(root, "no-such-directory", "cabinets.json"), root));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_cabinets_path_stops_startup(string path)
    {
        Assert.Throws<ArgumentException>(() =>
            new ServiceCollection().AddCabinetCatalog(path, root));
    }

    [Fact]
    public void The_catalog_registers_a_readiness_check()
    {
        var options = Provider(services =>
                services.AddCabinetCatalog(RepositoryFixtures.CabinetsFile, root))
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();

        var check = Assert.Single(options.Value.Registrations, registration => registration.Name == "cabinets");
        Assert.Contains("ready", check.Tags);
    }

    // --- the document store --------------------------------------------------------------------

    [Fact]
    public void The_store_is_registered_under_the_port_and_the_concrete_type()
    {
        // The rules depend on the port; the readiness probe needs the concrete store.
        var provider = Provider(services => services.AddDocumentsStore(root, root));

        Assert.Same(
            provider.GetRequiredService<IDocumentStore>(),
            provider.GetRequiredService<DocumentsStoreService>());
    }

    [Fact]
    public void The_store_is_registered_on_the_resolved_absolute_path()
    {
        var provider = Provider(services => services.AddDocumentsStore("documents", root));

        Assert.Equal(
            Path.Combine(root, "documents"),
            provider.GetRequiredService<DocumentsStoreService>().Root);
    }

    [Fact]
    public void Registering_the_store_prepares_its_directories_before_any_request()
    {
        new ServiceCollection().AddLogging().AddDocumentsStore(root, root);

        Assert.True(Directory.Exists(Path.Combine(root, "cabinets")));
        Assert.True(Directory.Exists(Path.Combine(root, "tmp")));
    }

    [Fact]
    public void Registering_the_store_sweeps_a_staging_directory_left_behind()
    {
        DocumentsStoreService.Prepare(root);
        Directory.CreateDirectory(Path.Combine(root, "tmp", "abandoned"));

        new ServiceCollection().AddLogging().AddDocumentsStore(root, root);

        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "tmp")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_storage_path_stops_startup(string path)
    {
        Assert.Throws<ArgumentException>(() =>
            new ServiceCollection().AddDocumentsStore(path, root));
    }

    [Fact]
    public void A_storage_path_that_cannot_be_created_stops_startup()
    {
        // A file where the directory should be: the closest portable stand-in for an unusable mount.
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "occupied");
        File.WriteAllBytes(file, []);

        Assert.ThrowsAny<IOException>(() =>
            new ServiceCollection().AddDocumentsStore(Path.Combine(file, "storage"), root));
    }

    [Fact]
    public void The_store_registers_a_readiness_check()
    {
        var options = Provider(services => services.AddDocumentsStore(root, root))
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>();

        var check = Assert.Single(options.Value.Registrations, registration => registration.Name == "storage");
        Assert.Contains("ready", check.Tags);
    }

    [Fact]
    public async Task Both_readiness_checks_report_healthy_on_a_usable_deployment()
    {
        // The closest a test can get to `/health/ready` without a container.
        var provider = Provider(services => services
            .AddCabinetCatalog(RepositoryFixtures.CabinetsFile, root)
            .AddDocumentsStore(root, root));

        var report = await provider.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Equal(["cabinets", "storage"], report.Entries.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Readiness_turns_unhealthy_when_the_storage_root_stops_being_writable()
    {
        var provider = Provider(services => services
            .AddCabinetCatalog(RepositoryFixtures.CabinetsFile, root)
            .AddDocumentsStore(root, root));

        Directory.Delete(Path.Combine(root, "tmp"), recursive: true);

        var report = await provider.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Equal(HealthStatus.Unhealthy, report.Entries["storage"].Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static IServiceProvider Provider(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        register(services);

        return services.BuildServiceProvider();
    }
}
