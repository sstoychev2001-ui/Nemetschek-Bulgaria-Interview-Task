using Nemetschek.Archive.Core.Utils.Documents;

namespace Nemetschek.Archive.Core.Services.Documents;

/// <summary>
/// The directory the Archive keeps everything in, and the preparation it needs before a request.
/// </summary>
public sealed class DocumentStorageRoot(string path)
{
    /// <summary>The absolute path of the storage root.</summary>
    public string Path { get; } = path;

    /// <summary>Creates the storage directories and clears anything left in flight.</summary>
    /// <exception cref="IOException">The storage root is not usable.</exception>
    public void Prepare()
    {
        Directory.CreateDirectory(
            System.IO.Path.Combine(Path, DocumentStorageUtils.CabinetsFolder));

        var staging = DocumentStorageUtils.Staging(Path);
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        Directory.CreateDirectory(staging);
    }
}
