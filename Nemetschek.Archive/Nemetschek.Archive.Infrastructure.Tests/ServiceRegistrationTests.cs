using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nemetschek.Archive.Core.Services.Documents;
using Nemetschek.Archive.Core.Services.Interfaces.Cabinets;
using Nemetschek.Archive.Core.Services.Interfaces.Documents;

namespace Nemetschek.Archive.Infrastructure.Tests;

/// <summary>
/// What the two <c>Add…</c> extensions wire up, and what they refuse to start without.
/// </summary>
public sealed class ServiceRegistrationTests : IDisposable
{
    private readonly string root =
        Path.Combine(Path.GetTempPath(), $"archive-registration-{Guid.NewGuid():N}");

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

        Assert.Equal(missing, failure.FileName);
    }

    [Fact]
    public void A_cabinets_path_whose_directory_is_not_there_also_stops_startup()
    {
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
    public void The_port_is_registered_but_not_the_store_itself()
    {
        var provider = Provider(services => services.AddDocumentsStore(root, root));

        Assert.NotNull(provider.GetRequiredService<IDocumentStore>());
        Assert.Null(provider.GetService<DocumentsStoreService>());
    }

    [Fact]
    public void A_relative_storage_path_is_resolved_against_the_base_path()
    {
        Provider(services => services.AddDocumentsStore("documents", root));

        Assert.True(Directory.Exists(Path.Combine(root, "documents", "cabinets")));
        Assert.True(Directory.Exists(Path.Combine(root, "documents", "tmp")));
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
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "occupied");
        File.WriteAllBytes(file, []);

        Assert.ThrowsAny<IOException>(() =>
            new ServiceCollection().AddDocumentsStore(Path.Combine(file, "storage"), root));
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
