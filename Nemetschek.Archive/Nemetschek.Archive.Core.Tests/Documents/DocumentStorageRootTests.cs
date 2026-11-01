using Nemetschek.Archive.Core.Services.Documents;

namespace Nemetschek.Archive.Core.Tests.Documents;

/// <summary>
/// Preparing the storage root, and whether it can be written to.
/// </summary>
public sealed class DocumentStorageRootTests : IDisposable
{
    private readonly string path =
        Path.Combine(Path.GetTempPath(), $"archive-root-{Guid.NewGuid():N}");

    private DocumentStorageRoot Root() => new(path);

    [Fact]
    public void Preparing_creates_the_directories_the_store_and_the_probe_need()
    {
        Root().Prepare();

        Assert.True(Directory.Exists(Path.Combine(path, "cabinets")));
        Assert.True(Directory.Exists(Path.Combine(path, "tmp")));
    }

    [Fact]
    public void Preparing_clears_a_staging_directory_left_by_an_upload_that_died()
    {
        Root().Prepare();
        Directory.CreateDirectory(Path.Combine(path, "tmp", "abandoned"));

        Root().Prepare();

        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(path, "tmp")));
    }

    [Fact]
    public void Preparing_keeps_the_documents_already_stored()
    {
        Root().Prepare();
        var kept = Path.Combine(path, "cabinets", "Invoices");
        Directory.CreateDirectory(kept);

        Root().Prepare();

        Assert.True(Directory.Exists(kept));
    }

    [Fact]
    public void A_root_that_cannot_be_created_refuses_to_prepare()
    {
        Directory.CreateDirectory(path);
        var file = Path.Combine(path, "occupied");
        File.WriteAllBytes(file, []);

        Assert.ThrowsAny<IOException>(() => new DocumentStorageRoot(file).Prepare());
    }

    [Fact]
    public void The_path_is_the_one_it_was_given()
    {
        Assert.Equal(path, Root().Path);
    }

    public void Dispose()
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
