using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Services.Interfaces.Documents;

namespace Nemetschek.Archive.Core.Services.Documents;

/// <summary>
/// An <see cref="IDocumentStore"/> that keeps each document as a directory of files, and logs what
/// it stored or could not store.
/// </summary>
public sealed class DocumentsStoreService(
    string rootPath,
    TimeProvider clock,
    ILogger<DocumentsStoreService> logger) : IDocumentStore
{
    private const string CabinetsFolder = "cabinets";
    private const string TempFolder = "tmp";
    private const string PartsFolder = "parts";

    private static readonly JsonSerializerOptions StoredJson =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The root of the Archive's storage.</summary>
    public string Root { get; } = rootPath;

    /// <summary>Creates the storage directories under a root and clears anything left in-flight.</summary>
    /// <exception cref="IOException">The storage root is not usable.</exception>
    public static void Prepare(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, CabinetsFolder));

        var temp = Path.Combine(root, TempFolder);
        if (Directory.Exists(temp))
        {
            Directory.Delete(temp, recursive: true);
        }

        Directory.CreateDirectory(temp);
    }

    /// <summary>Whether the storage root can actually be written to.</summary>
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
    public async Task<Document> AddAsync(Document document, CancellationToken cancellationToken)
    {
        var storedAt = clock.GetUtcNow();

        var staging = Path.Combine(Root, TempFolder, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(staging, PartsFolder));

        try
        {
            document = document with
            {
                Id = Guid.CreateVersion7(storedAt),
                Parts = await WritePartsAsync(document.Parts, staging, cancellationToken),
                StoredAt = storedAt,
                ModifiedAt = storedAt,
            };

            await using (var metadata = File.Create(MetadataFile(staging)))
            {
                await JsonSerializer.SerializeAsync(
                    metadata, document, StoredJson, cancellationToken);
            }

            var day = Directory.CreateDirectory(Path.Combine(
                CabinetDirectory(document.CabinetName),
                $"{storedAt.UtcDateTime:yyyy-MM-dd}"));

            Directory.Move(staging, Path.Combine(
                day.FullName,
                $"{storedAt.UtcDateTime:yyyyMMddTHHmmssfff}Z_{document.Id}"));

            logger.LogInformation(
                "Stored {DocumentId} in {CabinetName} with {PartCount} part(s)",
                document.Id,
                document.CabinetName,
                document.Parts.Count);

            return document;
        }
        catch (Exception exception)
        {
            Discard(staging);
            logger.LogError(
                exception, "Storing a document in {CabinetName} failed", document.CabinetName);
            throw;
        }
    }

    /// <inheritdoc />
    /// <exception cref="JsonException">A document's metadata is not valid JSON.</exception>
    /// <exception cref="ValidationException">The metadata does not describe a usable document.</exception>
    public async Task<DocumentPage> ListAsync(
        string cabinetName,
        StoredRange storedAt,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var directories = DocumentDirectories(cabinetName, storedAt).ToList();

        var documents = new List<Document>();
        foreach (var directory in directories.Skip(skip).Take(take))
        {
            documents.Add(await ReadAsync(directory, cancellationToken));
        }

        return new DocumentPage(documents, directories.Count);
    }

    /// <inheritdoc />
    public async Task<Document?> FindAsync(
        string cabinetName,
        Guid documentId,
        CancellationToken cancellationToken) =>
        DocumentDirectory(cabinetName, documentId) is { } directory
            ? await ReadAsync(directory, cancellationToken)
            : null;

    /// <inheritdoc />
    public Task<Stream?> OpenPartAsync(
        string cabinetName,
        Guid documentId,
        Guid partId,
        CancellationToken cancellationToken)
    {
        if (DocumentDirectory(cabinetName, documentId) is not { } directory)
        {
            return Task.FromResult<Stream?>(null);
        }

        var file = Directory
            .EnumerateFiles(Path.Combine(directory, PartsFolder), $"*_{partId}.bin")
            .FirstOrDefault();

        return Task.FromResult<Stream?>(file is null
            ? null
            : new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true));
    }

    /// <inheritdoc />
    public async Task<Document?> SetIndexValuesAsync(
        string cabinetName,
        Guid documentId,
        IReadOnlyDictionary<string, IndexValue> values,
        CancellationToken cancellationToken)
    {
        if (DocumentDirectory(cabinetName, documentId) is not { } directory)
        {
            return null;
        }

        var stored = await ReadAsync(directory, cancellationToken);

        var merged = new Dictionary<string, IndexValue>(
            stored.IndexValues, StringComparer.OrdinalIgnoreCase);
        foreach (var (field, value) in values)
        {
            merged[field] = value;
        }

        var updated = stored with { IndexValues = merged, ModifiedAt = clock.GetUtcNow() };
        var staging = Path.Combine(Root, TempFolder, $"{Guid.NewGuid():N}.json");

        try
        {
            await using (var metadata = File.Create(staging))
            {
                await JsonSerializer.SerializeAsync(metadata, updated, StoredJson, cancellationToken);
            }

            File.Move(staging, MetadataFile(directory), overwrite: true);
        }
        catch (Exception exception)
        {
            DiscardFile(staging);
            logger.LogError(
                exception,
                "Updating index values of {DocumentId} in {CabinetName} failed",
                documentId,
                cabinetName);

            throw;
        }

        logger.LogInformation(
            "Set {FieldCount} index value(s) on {DocumentId} in {CabinetName}",
            values.Count,
            documentId,
            cabinetName);

        return updated;
    }

    /// <summary>Every document directory of a cabinet inside a window, oldest first.</summary>
    // The directory names are the index: a day directory prunes a whole day without opening a file,
    // and the timestamp prefix inside it is the exact instant the document was stored, so the window
    // is applied without reading any metadata at all.
    private IEnumerable<string> DocumentDirectories(string cabinetName, StoredRange storedAt)
    {
        var cabinet = CabinetDirectory(cabinetName);
        if (!Directory.Exists(cabinet))
        {
            yield break;
        }

        foreach (var day in Directory.EnumerateDirectories(cabinet).Order(StringComparer.Ordinal))
        {
            if (DayOf(day) is { } utcDay && !storedAt.CouldIncludeDay(utcDay))
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(day)
                         .Order(StringComparer.Ordinal))
            {
                if (StoredAtOf(directory) is { } instant && !storedAt.Includes(instant))
                {
                    continue;
                }

                yield return directory;
            }
        }
    }

    /// <summary>The directory of one document, found by id, or <c>null</c>.</summary>
    // A scan of directory names, with no id index to keep consistent. An id carries its own
    // timestamp (Guid.CreateVersion7), so this can be narrowed to the day the id was minted in if a
    // cabinet ever grows enough for the scan to matter.
    private string? DocumentDirectory(string cabinetName, Guid documentId)
    {
        var cabinet = CabinetDirectory(cabinetName);
        if (!Directory.Exists(cabinet))
        {
            return null;
        }

        var suffix = $"_{documentId}";

        return Directory.EnumerateDirectories(cabinet)
            .Order(StringComparer.Ordinal)
            .SelectMany(Directory.EnumerateDirectories)
            .FirstOrDefault(directory =>
                Path.GetFileName(directory).EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<Document> ReadAsync(
        string directory,
        CancellationToken cancellationToken)
    {
        await using var metadata = File.OpenRead(MetadataFile(directory));

        return (await JsonSerializer.DeserializeAsync<Document>(
            metadata, cancellationToken: cancellationToken))!;
    }

    /// <summary>The UTC day a day directory holds, or <c>null</c> if its name is not one.</summary>
    private static DateOnly? DayOf(string dayDirectory) =>
        DateOnly.TryParseExact(Path.GetFileName(dayDirectory), "yyyy-MM-dd", out var day)
            ? day
            : null;

    /// <summary>The instant a document directory's name records, or <c>null</c>.</summary>
    private static DateTimeOffset? StoredAtOf(string documentDirectory)
    {
        var name = Path.GetFileName(documentDirectory);
        var separator = name.IndexOf('_', StringComparison.Ordinal);

        return separator > 0 && DateTimeOffset.TryParseExact(
            name[..separator],
            "yyyyMMddTHHmmssfffZ",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var storedAt)
            ? storedAt
            : null;
    }

    /// <exception cref="InvalidOperationException">A part has no content to read.</exception>
    private static async Task<IReadOnlyList<Part>> WritePartsAsync(
        IReadOnlyList<Part> drafts,
        string staging,
        CancellationToken cancellationToken)
    {
        var parts = new List<Part>(drafts.Count);

        for (var order = 0; order < drafts.Count; order++)
        {
            if (drafts[order].OpenContent is not { } openContent)
            {
                throw new InvalidOperationException(
                    $"Part '{drafts[order].Name}' has no content to store. A part on its way into " +
                    "the Archive must carry OpenContent.");
            }

            var part = drafts[order] with { Id = Guid.NewGuid(), Order = order };

            await using var source = openContent();
            await using var target = File.Create(Path.Combine(staging, PartPath(part)));

            await source.CopyToAsync(target, cancellationToken);

            parts.Add(part with { SizeInBytes = target.Length, OpenContent = null });
        }

        return parts;
    }

    private static string PartPath(Part part) =>
        $"{PartsFolder}/{part.Order:D3}_{part.Id}.bin";

    private static string MetadataFile(string documentDirectory) =>
        Path.Combine(documentDirectory, "document.json");

    /// <summary>Removes a staging file, keeping the failure that led here.</summary>
    private static void DiscardFile(string staging)
    {
        try
        {
            File.Delete(staging);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Removes a staging directory, keeping the failure that led here.</summary>
    private static void Discard(string staging)
    {
        try
        {
            Directory.Delete(staging, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Deleting it is best-effort: throwing here would replace the real failure with this pne
        }
    }

    private string CabinetDirectory(string cabinetName)
    {
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
}
