using System.ComponentModel.DataAnnotations;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nemetschek.Archive.Core.Domain.Documents;
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
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var cabinet = CabinetDirectory(cabinetName);
        var directories = new List<string>();

        if (Directory.Exists(cabinet))
        {
            directories.AddRange(Directory.EnumerateDirectories(cabinet)
                .Order(StringComparer.Ordinal)
                .SelectMany(day =>
                    Directory.EnumerateDirectories(day).Order(StringComparer.Ordinal)));
        }

        var documents = new List<Document>();
        foreach (var directory in directories.Skip(skip).Take(take))
        {
            await using var metadata = File.OpenRead(MetadataFile(directory));
            var stored = await JsonSerializer.DeserializeAsync<Document>(
                metadata, cancellationToken: cancellationToken);

            documents.Add(stored!);
        }

        return new DocumentPage(documents, directories.Count);
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
