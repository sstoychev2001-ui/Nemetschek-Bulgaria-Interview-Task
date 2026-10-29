using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Domain.Uploads;

/// <summary>
/// The folder-to-documents mapping of the upload rules: what an uploaded folder becomes, and what
/// is not stored.
/// </summary>
public sealed class UploadPlanningService
{
    /// <summary>
    /// Plans every entry of an uploaded folder, in one list, each carrying its state.
    /// </summary>
    public IReadOnlyList<PlannedDocument> PlanDocuments(
        Cabinet cabinet,
        IndexField field,
        UploadRequest request)
    {
        var parts = new Dictionary<string, List<PartDraft>>(StringComparer.Ordinal);
        var planned = new List<PlannedDocument>();

        foreach (var file in request.Files)
        {
            Place(file.RelativePath, file, parts, planned);
        }

        foreach (var folderPath in request.FolderPaths)
        {
            Place(folderPath, file: null, parts, planned);
        }

        planned.AddRange(parts
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => PlannedDocument.ToStore(new DocumentDraft(
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
    private static void Place(
        string path,
        UploadedFile? file,
        Dictionary<string, List<PartDraft>> parts,
        List<PlannedDocument> planned)
    {
        var segments = RelativePath.TrySplit(path);
        if (segments is null)
        {
            planned.Add(PlannedDocument.Skipped(
                path, $"'{path}' is not a path inside the uploaded folder, so it was not stored."));
            return;
        }

        if (!parts.TryGetValue(segments[0], out var document))
        {
            document = [];
            parts.Add(segments[0], document);
        }

        if (segments.Count > (file is null ? 1 : 2))
        {
            planned.Add(PlannedDocument.Skipped(
                path,
                $"'{path}' is nested more than one folder deep. Only the top level of the " +
                "uploaded folder and the files directly inside it are stored."));
            return;
        }

        if (file is not null)
        {
            document.Add(new PartDraft(segments[^1], file.OpenContent));
        }
    }
}
