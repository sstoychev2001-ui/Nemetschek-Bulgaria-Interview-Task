using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Core.Domain.Uploads;

/// <summary>
/// One entry of an uploaded folder, with what the upload rules decided about it.
/// </summary>
/// <param name="Entry">The document's title, or the relative path of an entry that was skipped.</param>
/// <param name="State">What the rules intend for the entry: <c>Stored</c> or <c>Skipped</c>.</param>
/// <param name="Draft">The document to store; <c>null</c> when the entry was skipped.</param>
/// <param name="Reason">Why it was skipped; empty when it is to be stored.</param>
// The state is the same enum the report ends up carrying, so an entry keeps one vocabulary from
// the plan to the response. Planning is the intent and storing is the verdict: a planned `Stored`
// either survives as `Stored` or becomes `Failed`, and a planned `Skipped` passes through
// untouched. `Failed` is therefore the one value planning never produces.
public sealed record PlannedDocument(
    string Entry,
    UploadEntryOutcome State,
    DocumentDraft? Draft,
    string Reason)
{
    /// <summary>An entry that becomes a document.</summary>
    public static PlannedDocument ToStore(DocumentDraft draft) =>
        new(draft.Title, UploadEntryOutcome.Stored, draft, Reason: "");

    /// <summary>An entry the upload rules do not store.</summary>
    public static PlannedDocument Skipped(string entry, string reason) =>
        new(entry, UploadEntryOutcome.Skipped, Draft: null, reason);
}
