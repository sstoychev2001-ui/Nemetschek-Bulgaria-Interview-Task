using Nemetschek.Archive.Core.Domain.Cabinets;

namespace Nemetschek.Archive.Api.Contracts.Uploads.Responses;

/// <summary>
/// What an upload did, as the client sees it.
/// </summary>
/// <param name="Cabinet">The file cabinet that was stored into.</param>
/// <param name="Field">The index field that received the folder's name.</param>
/// <param name="FolderName">The value written into that field on every stored document.</param>
/// <param name="Results">One result per entry the Archive saw, ordered by entry name.</param>
public sealed record UploadReportResponse(
    string Cabinet,
    string Field,
    string FolderName,
    IReadOnlyList<UploadEntryResult> Results);
