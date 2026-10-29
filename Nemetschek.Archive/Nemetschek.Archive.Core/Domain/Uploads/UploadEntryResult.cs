using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Core.Domain.Uploads;

/// <summary>
/// How one uploaded entry fared.
/// </summary>
/// <param name="Entry">The document's title, or the relative path of an entry that was skipped.</param>
/// <param name="Outcome">Whether it was stored, skipped or failed.</param>
/// <param name="DocumentId">The Archive's identifier, when it was stored.</param>
/// <param name="Parts">The names of its parts, in the order they were stored.</param>
/// <param name="Reason">Why it was skipped or failed; <c>null</c> when it was stored.</param>
public sealed record UploadEntryResult(
    string Entry,
    UploadEntryOutcome Outcome,
    Guid? DocumentId,
    IReadOnlyList<string> Parts,
    string? Reason)
{
    /// <summary>A document that reached the Archive.</summary>
    public static UploadEntryResult Stored(Document document) => new(
        document.Title,
        UploadEntryOutcome.Stored,
        document.Id,
        [.. document.Parts.Select(part => part.Name)],
        Reason: null);

    /// <summary>An entry the upload rules do not store.</summary>
    public static UploadEntryResult Skipped(string entry, string reason) => new(
        entry, UploadEntryOutcome.Skipped, DocumentId: null, Parts: [], reason);

    /// <summary>A document that was attempted and did not reach the Archive.</summary>
    public static UploadEntryResult Failed(string title, string reason) => new(
        title, UploadEntryOutcome.Failed, DocumentId: null, Parts: [], reason);
}
