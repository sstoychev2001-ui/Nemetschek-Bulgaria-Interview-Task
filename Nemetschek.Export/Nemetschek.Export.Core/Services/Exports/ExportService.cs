using System.Globalization;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Nemetschek.Export.Core.Domain.Exports;
using Nemetschek.Export.Core.Services.Interfaces;

namespace Nemetschek.Export.Core.Services.Exports;

/// <summary>
/// Runs an export: reads the matching documents from the Archive, writes them into a zip archive by
/// the export rules, and marks each one as it goes.
/// </summary>
public sealed class ExportService(
    IArchiveClient archive,
    TimeProvider clock,
    ILogger<ExportService> logger)
{
    /// <summary>
    /// Builds the export and returns it as a stream positioned at the start, ready to be read. The
    /// caller owns the stream and disposes it; doing so also removes the file behind it.
    /// </summary>
    public async Task<Stream> WriteAsync(
        ExportRequest request,
        CancellationToken cancellationToken)
    {
        var marker =$"Exported on {clock.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        var titles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var exported = new List<Guid>();
        var files = 0;

        var destination = new FileStream(
            Path.Combine(Path.GetTempPath(), $"export-{Guid.NewGuid():N}.zip"),
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.ReadWrite,
                Options = FileOptions.DeleteOnClose | FileOptions.Asynchronous,
            });

        try
        {
            using (var zip = new ZipArchive(destination, ZipArchiveMode.Create, true))
            {
                var found = archive.ReadDocumentsAsync(
                    request.CabinetName, request.From, request.To, cancellationToken);

                await foreach (var document in found)
                {
                    files += await WriteDocumentAsync(
                        zip,
                        GetEntryName(document.Title, titles),
                        request.CabinetName,
                        document,
                        cancellationToken);
                    exported.Add(document.Id);
                }
            }

            if (exported.Count > 0)
            {
                await archive.SetTextIndexValueAsync(
                    request.CabinetName,
                    exported,
                    request.MarkerFieldName,
                    marker,
                    cancellationToken);
            }

            logger.LogInformation(
                "Exported {DocumentCount} document(s) and {FileCount} file(s), each marked",
                exported.Count,
                files);

            destination.Position = 0;
            return destination;
        }
        catch
        {
            await destination.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Writes one document into the archive and returns how many files it became: one for a
    /// single-part document, one per part for a folder, and none for an empty folder.
    /// </summary>
    private async Task<int> WriteDocumentAsync(
        ZipArchive zip,
        string entryName,
        string cabinetName,
        ArchiveDocument document,
        CancellationToken cancellationToken)
    {
        if (document.Parts.Count == 0)
        {
            zip.CreateEntry($"{entryName}/");

            return 0;
        }

        if (document.Parts.Count == 1)
        {
            await using var content = await archive.OpenPartAsync(
                cabinetName, document.Id, document.Parts[0].Id, cancellationToken);
            await using var entry = zip.CreateEntry(entryName).Open();

            await content.CopyToAsync(entry, cancellationToken);

            return 1;
        }

        var partNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in document.Parts)
        {
            await using var content = await archive.OpenPartAsync(
                cabinetName, document.Id, part.Id, cancellationToken);
            await using var entry = zip
                .CreateEntry($"{entryName}/{GetEntryName(part.Name, partNames)}")
                .Open();

            await content.CopyToAsync(entry, cancellationToken);
        }

        return document.Parts.Count;
    }

    /// <summary>
    /// A name for an entry that nothing in <paramref name="taken"/> has used yet, remembering it.
    /// </summary>
    private static string GetEntryName(string name, Dictionary<string, int> taken)
    {
        var extension = Path.GetExtension(name);
        var candidate = name;

        while (!taken.TryAdd(candidate, 1))
        {
            candidate = $"{name[..^extension.Length]} ({++taken[name]}){extension}";
        }

        return candidate;
    }
}