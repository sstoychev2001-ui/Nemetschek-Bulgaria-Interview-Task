using Nemetschek.Archive.Core.Domain.Uploads;

namespace Nemetschek.Archive.Api.Contracts.Uploads;

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
    IReadOnlyList<UploadEntryResult> Results)
{
    /// <summary>Maps the Archive's report onto the wire shape.</summary>
    public static UploadReportResponse From(UploadReport report) => new(
        report.CabinetName,
        report.FieldName,
        report.FolderName,
        [.. report.Results.Select(UploadEntryResult.From)]);
}
