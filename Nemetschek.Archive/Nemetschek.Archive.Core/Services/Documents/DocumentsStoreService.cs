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
    string storagePath,
    TimeProvider clock,
    ILogger<DocumentsStoreService> logger) : IDocumentStore
{
    private const int DefaultReadBufferSize = 64 * 1024;

    /// <inheritdoc />
    public async Task<Document> AddAsync(Document document, CancellationToken cancellationToken)
    {
        var storedAt = clock.GetUtcNow();

        var staging = DocumentStorageUtils.StagingPath(storagePath);
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
                DocumentStorageUtils.CabinetDirectory(storagePath, document.CabinetName),
                storedAt.UtcDateTime.ToString(DocumentStorageUtils.DayFormat, CultureInfo.InvariantCulture))
            );

            var destinationDirectoryName = Path.Combine(
                day.FullName,
                $"{storedAt.UtcDateTime.ToString(DocumentStorageUtils.InstantFormat, CultureInfo.InvariantCulture)}_{document.Id}");

            Directory.Move(staging, destinationDirectoryName);

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
        var allDirectories = DocumentDirectories(cabinetName, storedAt)
            .ToList();

        var directoryPage = allDirectories
            .Skip(skip)
            .Take(take);

        var documents = new List<Document>();
        foreach (var directory in directoryPage)
        {
            documents.Add(await DocumentMetadataFile.ReadAsync(directory, cancellationToken));
        }

        return new DocumentPage(documents, allDirectories.Count);
    }

    /// <inheritdoc />
    public async Task<Document> FindAsync(string cabinetName, Guid documentId, CancellationToken cancellationToken) =>
        await DocumentMetadataFile.ReadAsync(GetDocumentDirectory(cabinetName, documentId), cancellationToken);

    /// <inheritdoc />
    public Task<Stream> OpenPartAsync(string cabinetName,
        Guid documentId,
        Guid partId,
        CancellationToken cancellationToken)
    {
        var partPath = DocumentStorageUtils.PartsDirectory(GetDocumentDirectory(cabinetName, documentId));

        var file = Directory
            .EnumerateFiles(partPath, $"*_{partId}.bin")
            .FirstOrDefault()
            ?? throw new DocumentNotFoundException(cabinetName, documentId, partId);

        var partStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultReadBufferSize, true);

        return Task.FromResult<Stream>(partStream);
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

        var staging = DocumentStorageUtils.StagingPath(storagePath, ".json");

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
        var cabinet = DocumentStorageUtils.CabinetDirectory(storagePath, cabinetName);
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
        var cabinet = DocumentStorageUtils.CabinetDirectory(storagePath, cabinetName);

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

            var id = Guid.NewGuid();

            await using var source = openContent();
            await using var target = File.Create(Path.Combine(
                staging, DocumentStorageUtils.PartsFolder, $"{order:D3}_{id}.bin"));

            await source.CopyToAsync(target, cancellationToken);

            parts.Add(drafts[order] with
            {
                Id = id,
                Order = order,
                SizeInBytes = target.Length,
                OpenContent = null
            });
        }

        return parts;
    }
}
