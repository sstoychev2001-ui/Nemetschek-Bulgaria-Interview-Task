using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text.Json;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Ports;

namespace Nemetschek.Archive.Infrastructure.Documents;

/// <summary>
/// An <see cref="IDocumentStore"/> that keeps each document as a directory of files.
/// </summary>
// Durable by being nothing but files on a mounted volume: there is no in-memory state, so a
// restart has nothing to lose and no cache to rebuild. The layout is owned here, and only here:
//
//   <root>/cabinets/<Cabinet>/<yyyy-MM-dd>/<yyyyMMddTHHmmssfffZ>_<documentId>/
//                                            document.json
//                                            parts/000_<partId>.bin   (named by PartFile)
//   <root>/tmp/<staging>/                  in-flight writes only, swept at startup
//
// The day directory makes a date range a directory prune rather than a scan, and the timestamp
// prefix makes an ordinal sort of the names chronological — so a page is chosen from directory
// names alone, without opening a single document.
public sealed class FileSystemDocumentStore(string rootPath, TimeProvider clock) : IDocumentStore
{
    private const string CabinetsFolder = "cabinets";
    private const string TempFolder = "tmp";

    private static readonly JsonSerializerOptions StoredJson =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The root of the Archive's storage.</summary>
    public string Root { get; } = rootPath;

    /// <summary>Creates the storage directories and clears anything left in-flight.</summary>
    /// <exception cref="IOException">The storage root is not usable.</exception>
    // Called once at startup. A staging directory can only survive a process that died mid-upload:
    // the document it belonged to was never committed, so deleting it is the recovery.
    public void Prepare()
    {
        Directory.CreateDirectory(Path.Combine(Root, CabinetsFolder));

        var temp = Path.Combine(Root, TempFolder);
        if (Directory.Exists(temp))
        {
            Directory.Delete(temp, recursive: true);
        }

        Directory.CreateDirectory(temp);
    }

    /// <summary>Whether the storage root can actually be written to.</summary>
    // Probed by the readiness check: a volume that is missing or owned by another user would
    // otherwise show up as every upload failing, rather than as a service saying it is not ready.
    public bool CanWrite()
    {
        try
        {
            var probe = Path.Combine(Root, TempFolder, $"probe-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<Document> AddAsync(DocumentDraft draft, CancellationToken cancellationToken)
    {
        // StoredAt and ModifiedAt are UTC and set here: the system fields are the Archive's, never
        // the client's. A fresh document has not been modified since it was stored, so they match.
        var storedAt = clock.GetUtcNow();

        var staging = Path.Combine(Root, TempFolder, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(staging, PartFile.PartsFolder));

        try
        {
            // Version 7 is time-ordered, so the id carries its own timestamp. That is what lets a
            // document be found later from its id alone, with no second index to keep in step.
            var document = new Document(
                Guid.CreateVersion7(storedAt),
                draft.CabinetName,
                draft.Title,
                draft.IndexValues,
                await WritePartsAsync(draft, staging, cancellationToken),
                storedAt,
                storedAt);

            await using (var metadata = File.Create(MetadataFile(staging)))
            {
                await JsonSerializer.SerializeAsync(
                    metadata, DocumentFile.From(document), StoredJson, cancellationToken);
            }

            // The whole document arrives in one atomic rename, so it is either completely there or
            // not there at all. A reader can never see a half-written document.
            var day = Directory.CreateDirectory(Path.Combine(
                CabinetDirectory(document.CabinetName),
                $"{storedAt.UtcDateTime:yyyy-MM-dd}"));

            Directory.Move(staging, Path.Combine(
                day.FullName,
                $"{storedAt.UtcDateTime:yyyyMMddTHHmmssfff}Z_{document.Id}"));

            return document;
        }
        catch
        {
            // Nothing was committed, so nothing should be left behind: the brief is explicit that
            // an operation must not leave temporary files growing forever.
            Discard(staging);
            throw;
        }
    }

    /// <inheritdoc />
    /// <exception cref="JsonException">A document's metadata is not valid JSON.</exception>
    /// <exception cref="ValidationException">The metadata does not describe a usable document.</exception>
    public async Task<DocumentPage> ListAsync(
        string cabinetName,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var cabinet = CabinetDirectory(cabinetName);
        var directories = new List<string>();

        if (Directory.Exists(cabinet))
        {
            // Directory names carry the sort key, so the page is chosen before any file is opened
            // and only the documents actually on it are read. Ordinal, and both name formats are
            // zero-padded, so the order cannot depend on the container's locale.
            directories.AddRange(Directory.EnumerateDirectories(cabinet)
                .Order(StringComparer.Ordinal)
                .SelectMany(day =>
                    Directory.EnumerateDirectories(day).Order(StringComparer.Ordinal)));
        }

        var documents = new List<Document>();
        foreach (var directory in directories.Skip(skip).Take(take))
        {
            await using var metadata = File.OpenRead(MetadataFile(directory));
            var stored = await JsonSerializer.DeserializeAsync<DocumentFile>(
                metadata, cancellationToken: cancellationToken);

            documents.Add(stored!.ToDocument());
        }

        return new DocumentPage(documents, directories.Count);
    }

    private static async Task<IReadOnlyList<Part>> WritePartsAsync(
        DocumentDraft draft,
        string staging,
        CancellationToken cancellationToken)
    {
        var parts = new List<Part>(draft.Parts.Count);

        for (var order = 0; order < draft.Parts.Count; order++)
        {
            var part = new Part(Guid.NewGuid(), draft.Parts[order].Name, order, SizeInBytes: 0);

            // The same PartFile.PathFor that writes the location into document.json, so the file
            // on disk and the metadata pointing at it cannot disagree.
            await using var source = draft.Parts[order].OpenContent();
            await using var target = File.Create(Path.Combine(staging, PartFile.PathFor(part)));

            // Copied, never buffered: an upload may be far larger than the memory available. Both
            // streams are opened and disposed here, so a failure leaves no handle on a temp file.
            await source.CopyToAsync(target, cancellationToken);

            parts.Add(part with { SizeInBytes = target.Length });
        }

        return parts;
    }

    private static string MetadataFile(string documentDirectory) =>
        Path.Combine(documentDirectory, "document.json");

    private string CabinetDirectory(string cabinetName)
    {
        // A cabinet name comes from cabinets.json rather than from a user, but it is the one name
        // in this layout that is not server-generated, so it is checked rather than trusted.
        // Everything else in a path here is a GUID or a timestamp.
        if (cabinetName != Path.GetFileName(cabinetName) ||
            cabinetName is "" or "." or ".." ||
            cabinetName.Any(Path.GetInvalidFileNameChars().Contains))
        {
            throw new ArgumentException(
                $"File cabinet name '{cabinetName}' cannot be used as a directory name.",
                nameof(cabinetName));
        }

        return Path.Combine(Root, CabinetsFolder, cabinetName);
    }

    private static void Discard(string staging)
    {
        try
        {
            Directory.Delete(staging, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The document already failed; losing the cleanup must not replace the real reason it
            // failed. Whatever is left is swept the next time the service starts.
        }
    }
}
