using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Domain;

/// <summary>
/// One entry of an uploaded folder, carried from the plan through to the report. Planning sets
/// the intent, storing records what became of it.
/// </summary>
/// <param name="Entry">The document's title.</param>
/// <param name="Outcome">The outcome of the storage operation.</param>
/// <param name="Draft">The document to store; <c>null</c> once it has been stored or was not.</param>
/// <param name="DocumentId">The Archive's identifier, once it has been stored.</param>
/// <param name="Parts">The names of its parts, in the order they were stored.</param>
/// <param name="Reason">Why it was skipped or failed; <c>null</c> when it was stored.</param>
public sealed record UploadEntry(
    string Entry,
    UploadEntryOutcome Outcome,
    Document? Draft,
    Guid? DocumentId,
    IReadOnlyList<string> Parts,
    string? Reason)
{
    /// <summary>An entry the rules intend to store, before it reaches the Archive.</summary>
    public static UploadEntry ToStore(Document draft) => new(
        draft.Title, UploadEntryOutcome.Stored, draft, DocumentId: null, Parts: [], Reason: null);

    /// <summary>An entry the upload rules do not store.</summary>
    public static UploadEntry Skipped(string entry, string reason) => new(
        entry, UploadEntryOutcome.Skipped, Draft: null, DocumentId: null, Parts: [], reason);

    /// <summary>An entry that never reached the Archive.</summary>
    public static UploadEntry Failed(string entry, string reason) => new(
        entry, UploadEntryOutcome.Failed, Draft: null, DocumentId: null, Parts: [], reason);
}
