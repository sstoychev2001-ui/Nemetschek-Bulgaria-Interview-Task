using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Services;

/// <summary>
/// The folder-to-documents mapping of the upload rules: what an uploaded folder becomes, and what
/// is not stored.
/// </summary>
public sealed class UploadPlanningService
{
    private const int MaxFolderSegments = 1;
    private const int MaxFileSegments = 2;

    /// <summary>
    /// Plans every entry of an uploaded folder, in one list, each carrying its outcome.
    /// </summary>
    public IReadOnlyList<UploadEntry> PlanEntries(
        Cabinet cabinet,
        IndexField field,
        UploadRequest request)
    {
        var parts = new Dictionary<string, List<Part>>(StringComparer.Ordinal);
        var planned = new List<UploadEntry>();

        foreach (var file in request.Files)
        {
            Place(file.RelativePath, file, MaxFileSegments, parts, planned);
        }

        foreach (var folderPath in request.FolderPaths)
        {
            Place(folderPath, file: null, MaxFolderSegments, parts, planned);
        }

        planned.AddRange(parts
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => UploadEntry.ToStore(Document.ToStore(
                cabinet.Name,
                entry.Key,
                new Dictionary<string, IndexValue>(StringComparer.OrdinalIgnoreCase)
                {
                    [field.Name] = new IndexValue.Text(request.FolderName),
                },
                [.. entry.Value.OrderBy(part => part.Name, StringComparer.Ordinal)]))));

        return planned;
    }

    /// <summary>
    /// Places one uploaded entry — a file when <paramref name="file"/> is given, otherwise a
    /// folder — into the document it belongs to, or records why it is not stored.
    /// </summary>
    /// <param name="path">The entry's path relative to the uploaded folder. Untrusted.</param>
    /// <param name="file">The uploaded file, or <c>null</c> when the entry is a folder.</param>
    /// <param name="maxSegments">
    /// How deep this kind of entry may sit: <see cref="MaxFileSegments"/> for a file,
    /// <see cref="MaxFolderSegments"/> for a folder.
    /// </param>
    /// <param name="parts">The parts gathered so far, keyed by the document's title.</param>
    /// <param name="planned">The entries planned so far, which this one is added to.</param>
    private static void Place(
        string path,
        UploadedFile? file,
        int maxSegments,
        Dictionary<string, List<Part>> parts,
        List<UploadEntry> planned)
    {
        var segments = RelativePath.TrySplit(path);
        if (segments is null)
        {
            planned.Add(UploadEntry.Skipped(
                path, $"'{path}' is not a path inside the uploaded folder, so it was not stored."));
            return;
        }

        if (!parts.TryGetValue(segments[0], out var document))
        {
            document = [];
            parts.Add(segments[0], document);
        }

        if (segments.Count > maxSegments)
        {
            planned.Add(UploadEntry.Skipped(path,$"'{path}' is nested more than one folder deep."));
            return;
        }

        if (file is not null)
        {
            document.Add(Part.ToStore(segments[^1], file.OpenContent));
        }
    }
}
