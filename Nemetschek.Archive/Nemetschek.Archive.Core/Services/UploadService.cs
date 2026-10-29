using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Services;

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
    /// <exception cref="UnknownCabinetException">There is no such file cabinet.</exception>
    /// <exception cref="UploadRefusedException">The request breaks an index field rule.</exception>
    public async Task<UploadReport> UploadAsync(
        UploadRequest request,
        CancellationToken cancellationToken)
    {
        if (cabinets.Find(request.CabinetName) is not { } cabinet)
        {
            throw new UnknownCabinetException(request.CabinetName);
        }

        Refuse(cabinet, request);

        return await StoreAsync(cabinet, request, cancellationToken);
    }

    /// <summary>
    /// Checks everything that must hold before anything is stored, and throws if it does not. A
    /// refusal here is terminal and there is only ever one, which is why it throws where a
    /// per-entry refusal is a value in the report.
    /// </summary>
    /// <exception cref="UploadRefusedException">The request breaks an index field rule.</exception>
    private static void Refuse(Cabinet cabinet, UploadRequest request)
    {
        if (IndexFieldRules.ValidateChosenTextField(cabinet, request.FieldName) is { } notChoosable)
        {
            throw new UploadRefusedException(notChoosable.FieldName, notChoosable.Reason);
        }

        if (!RelativePath.IsName(request.FolderName))
        {
            throw new UploadRefusedException(
                request.FieldName,
                $"'{request.FolderName}' is not a usable folder name: it must be a single name, " +
                "without path separators or control characters.");
        }

        if (IndexFieldRules.ValidateWrite(
                cabinet,
                request.FieldName,
                new IndexValue.Text(request.FolderName)) is { } violation)
        {
            throw new UploadRefusedException(violation.FieldName, violation.Reason);
        }
    }

    private async Task<UploadReport> StoreAsync(
        Cabinet cabinet,
        UploadRequest request,
        CancellationToken cancellationToken)
    {
        var field = cabinet.Fields.First(candidate =>
            string.Equals(candidate.Name, request.FieldName, StringComparison.OrdinalIgnoreCase));

        var results = new List<UploadEntry>();
        var plannedEntries = planningService.PlanEntries(cabinet, field, request);

        foreach (var plannedEntry in plannedEntries)
        {
            UploadEntry entry;
            if (plannedEntry.Draft is { } draft)
            {
                entry = await StoreOneAsync(plannedEntry, draft, cancellationToken);
            }
            else
            {
                entry = plannedEntry;
            }

            results.Add(entry);
        }

        results.Sort((left, right) => string.CompareOrdinal(left.Entry, right.Entry));

        return new UploadReport(cabinet.Name, field.Name, request.FolderName, results);
    }

    private async Task<UploadEntry> StoreOneAsync(
        UploadEntry planned,
        Document draft,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = await documents.AddAsync(draft, cancellationToken);

            return planned with
            {
                Draft = null,
                DocumentId = document.Id,
                Parts = [.. document.Parts.Select(part => part.Name)],
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return planned with
            {
                Outcome = UploadEntryOutcome.Failed,
                Draft = null,
                Reason = exception.Message,
            };
        }
    }
}
