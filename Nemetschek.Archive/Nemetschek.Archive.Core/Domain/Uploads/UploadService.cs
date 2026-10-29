using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Ports;

namespace Nemetschek.Archive.Core.Domain.Uploads;

/// <summary>
/// Runs an upload: checks the request against the index field rules, maps the folder onto
/// documents, stores them one at a time and reports what happened to each.
/// </summary>
public sealed class UploadService(
    ICabinetCatalog cabinets,
    IDocumentStore documents,
    UploadPlanningService planningService)
{
    /// <summary>Stores an uploaded folder and reports what happened to every entry.</summary>
    public async Task<UploadReport> UploadAsync(
        UploadRequest request,
        CancellationToken cancellationToken)
    {
        var cabinet = cabinets.Find(request.CabinetName);

        if (Refuse(cabinet, request) is { } refusal)
        {
            return refusal;
        }

        return await StoreAsync(cabinet!, request, cancellationToken);
    }

    /// <summary>
    /// Checks everything that must hold before anything is stored. Returns <c>null</c> when the
    /// request is acceptable, otherwise the report refusing it. A cabinet that was not found is
    /// always refused, so a <c>null</c> return also means <paramref name="cabinet"/> is present.
    /// </summary>
    private static UploadReport? Refuse(Cabinet? cabinet, UploadRequest request)
    {
        string reason;

        if (cabinet is null)
        {
            reason = $"There is no file cabinet named '{request.CabinetName}'.";
        }
        else if (IndexFieldRules.ValidateChosenTextField(cabinet, request.FieldName)
                 is { } notChoosable)
        {
            reason = notChoosable.Reason;
        }
        else if (!RelativePath.IsName(request.FolderName))
        {
            reason =
                $"'{request.FolderName}' is not a usable folder name: it must be a single name, " +
                "without path separators or control characters.";
        }
        else if (IndexFieldRules.ValidateWrite(
                     cabinet,
                     request.FieldName,
                     new IndexValue.Text(request.FolderName)) is { } violation)
        {
            reason = violation.Reason;
        }
        else
        {
            return null;
        }

        return new UploadReport(
            request.CabinetName,
            request.FieldName,
            request.FolderName,
            [UploadEntryResult.Failed(request.FolderName, reason)]);
    }

    private async Task<UploadReport> StoreAsync(
        Cabinet cabinet,
        UploadRequest request,
        CancellationToken cancellationToken)
    {
        var field = cabinet.Fields.First(candidate =>
            string.Equals(candidate.Name, request.FieldName, StringComparison.OrdinalIgnoreCase));

        var results = new List<UploadEntryResult>();
        var plannedDocuments = planningService.PlanDocuments(cabinet, field, request);

        foreach (var plannedDocument in plannedDocuments)
        {
            results.Add(plannedDocument.State is UploadEntryOutcome.Stored
                        && plannedDocument.Draft is { } draft
                ? await StoreOneAsync(draft, cancellationToken)
                : UploadEntryResult.Skipped(plannedDocument.Entry, plannedDocument.Reason));
        }

        results.Sort((left, right) => string.CompareOrdinal(left.Entry, right.Entry));

        return new UploadReport(cabinet.Name, field.Name, request.FolderName, results);
    }

    private async Task<UploadEntryResult> StoreOneAsync(
        DocumentDraft draft,
        CancellationToken cancellationToken)
    {
        try
        {
            return UploadEntryResult.Stored(await documents.AddAsync(draft, cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return UploadEntryResult.Failed(draft.Title, exception.Message);
        }
    }
}
