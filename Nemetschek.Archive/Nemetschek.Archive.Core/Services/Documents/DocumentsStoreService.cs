using System.Globalization;
using Microsoft.Extensions.Logging;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Exceptions.Documents;
using Nemetschek.Archive.Core.Services.Interfaces.Documents;
using Nemetschek.Archive.Core.Utils.Documents;

namespace Nemetschek.Archive.Core.Services.Documents;

/// <summary>
/// An <see cref="IDocumentStore"/> that keeps each document as a directory of files, and logs what
/// it stored or could not store.
/// </summary>
public sealed class DocumentsStoreService(
    DocumentStorageRoot root,
    TimeProvider clock,
    ILogger<DocumentsStoreService> logger) : IDocumentStore
{
    /// <inheritdoc />
    public async Task<Document> AddAsync(Document document, CancellationToken cancellationToken)
    {
        var storedAt = clock.GetUtcNow();

        var staging = DocumentStorageUtils.StagingPath(root.Path);
        Directory.CreateDirectory(DocumentStorageUtils.PartsDirectory(staging));

        try
        {
            document = document with
            {
                Id = Guid.CreateVersion7(storedAt),
                Parts = await WritePartsAsync(document.Parts, staging, cancellationToken),
                StoredAt = storedAt,
                ModifiedAt = storedAt,
            };

            await DocumentMetadataFile.WriteAsync(
                DocumentStorageUtils.MetadataFile(staging), document, cancellationToken);

            var day = Directory.CreateDirectory(Path.Combine(
                DocumentStorageUtils.CabinetDirectory(root.Path, document.CabinetName),
                storedAt.UtcDateTime.ToString(
                    DocumentStorageUtils.DayFormat, CultureInfo.InvariantCulture)));

            Directory.Move(
                staging,
                Path.Combine(
                    day.FullName,
                    $"{storedAt.UtcDateTime.ToString(
                        DocumentStorageUtils.InstantFormat, CultureInfo.InvariantCulture)}" +
                    $"_{document.Id}"));

            logger.LogInformation(
                "Stored {DocumentId} in {CabinetName} with {PartCount} part(s)",
                document.Id,
                document.CabinetName,
                document.Parts.Count);

            return document;
        }
        catch (Exception exception)
        {
            Directory.Delete(staging, recursive: true);
            logger.LogError(
                exception, "Storing a document in {CabinetName} failed", document.CabinetName);

            throw;
        }
    }

    /// <inheritdoc />
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
            documents.Add(await DocumentMetadataFile.ReadAsync(directory, cancellationToken));
        }

        return new DocumentPage(documents, directories.Count);
    }

    /// <inheritdoc />
    public async Task<Document> FindAsync(
        string cabinetName,
        Guid documentId,
        CancellationToken cancellationToken) =>
        await DocumentMetadataFile.ReadAsync(GetDocumentDirectory(cabinetName, documentId), cancellationToken);

    /// <inheritdoc />
    public Task<Stream> OpenPartAsync(
        string cabinetName,
        Guid documentId,
        Guid partId,
        CancellationToken cancellationToken)
    {
        var file = Directory
            .EnumerateFiles(
                DocumentStorageUtils.PartsDirectory(
                    GetDocumentDirectory(cabinetName, documentId)),
                $"*_{partId}.bin")
            .FirstOrDefault()
            ?? throw new DocumentNotFoundException(cabinetName, documentId, partId);

        return Task.FromResult<Stream>(new FileStream(
            file,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true));
    }

    /// <inheritdoc />
    public async Task SetIndexValuesAsync(
        string cabinetName,
        Guid documentId,
        IReadOnlyDictionary<string, IndexValue> values,
        CancellationToken cancellationToken)
    {
        var directory = GetDocumentDirectory(cabinetName, documentId);
        var stored = await DocumentMetadataFile.ReadAsync(directory, cancellationToken);

        var merged = new Dictionary<string, IndexValue>(stored.IndexValues, StringComparer.OrdinalIgnoreCase);
        foreach (var (field, value) in values)
        {
            merged[field] = value;
        }

        var updated = stored with
        {
            IndexValues = merged,
            ModifiedAt = clock.GetUtcNow()
        };

        var staging = DocumentStorageUtils.StagingPath(root.Path, ".json");

        try
        {
            await DocumentMetadataFile.WriteAsync(staging, updated, cancellationToken);

            File.Move(staging, DocumentStorageUtils.MetadataFile(directory), true);
        }
        catch (Exception exception)
        {
            File.Delete(staging);
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
    }

    /// <summary>Every document directory of a cabinet inside a window, oldest first.</summary>
    private IEnumerable<string> DocumentDirectories(string cabinetName, StoredRange storedAt)
    {
        var cabinet = DocumentStorageUtils.CabinetDirectory(root.Path, cabinetName);
        if (!Directory.Exists(cabinet))
        {
            yield break;
        }

        foreach (var day in Directory.EnumerateDirectories(cabinet).Order(StringComparer.Ordinal))
        {
            var dateDirectory = DocumentStorageUtils.DayOf(day);
            if (dateDirectory is not null && !storedAt.CouldIncludeDay(dateDirectory.Value))
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(day).Order(StringComparer.Ordinal))
            {
                var storedAtDirectory = DocumentStorageUtils.StoredAtOf(directory);
                if (storedAtDirectory is not null && !storedAt.Includes(storedAtDirectory.Value))
                {
                    continue;
                }

                yield return directory;
            }
        }
    }

    /// <summary>The directory of one document, found by id.</summary>
    /// <exception cref="DocumentNotFoundException">The cabinet does not hold it.</exception>
    private string GetDocumentDirectory(string cabinetName, Guid documentId)
    {
        var cabinet = DocumentStorageUtils.CabinetDirectory(root.Path, cabinetName);

        if (Directory.Exists(cabinet))
        {
            var documentDirectory = Directory.EnumerateDirectories(cabinet)
                .Order(StringComparer.Ordinal)
                .SelectMany(Directory.EnumerateDirectories)
                .FirstOrDefault(directory => Path.GetFileName(directory)
                    .EndsWith($"_{documentId}", StringComparison.OrdinalIgnoreCase));

            if (documentDirectory is null)
            {
                throw new DocumentNotFoundException(cabinetName, documentId);
            }

            return documentDirectory;
        }

        throw new DocumentNotFoundException(cabinetName, documentId);
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

            var part = drafts[order] with
            {
                Id = Guid.NewGuid(),
                Order = order
            };

            await using var source = openContent();
            await using var target = File.Create(Path.Combine(
                staging, DocumentStorageUtils.PartsFolder, $"{part.Order:D3}_{part.Id}.bin"));

            await source.CopyToAsync(target, cancellationToken);

            parts.Add(part with
            {
                SizeInBytes = target.Length,
                OpenContent = null
            });
        }

        return parts;
    }
}
