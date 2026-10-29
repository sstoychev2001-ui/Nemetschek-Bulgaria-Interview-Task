using Nemetschek.Archive.Core.Domain.Uploads;

namespace Nemetschek.Archive.Api.Contracts.Uploads.Responses;

/// <summary>
/// How one uploaded entry fared.
/// </summary>
/// <param name="Entry">The document's title, or the relative path of an entry that was skipped.</param>
/// <param name="Outcome">Whether it was <c>Stored</c>, <c>Skipped</c> or <c>Failed</c>.</param>
/// <param name="DocumentId">The Archive's identifier, when it was stored.</param>
/// <param name="Parts">The names of its parts, in the order they were stored.</param>
/// <param name="Reason">Why it was skipped or failed; <c>null</c> when it was stored.</param>
public sealed record UploadEntryResult(
    string Entry,
    UploadEntryOutcome Outcome,
    Guid? DocumentId,
    IReadOnlyList<string> Parts,
    string? Reason);
