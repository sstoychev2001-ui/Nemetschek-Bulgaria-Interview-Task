using Nemetschek.Archive.Api.Contracts.Uploads.Responses;
using Nemetschek.Archive.Core.Domain.Uploads;

namespace Nemetschek.Archive.Api.Contracts.Uploads.Mappings;

/// <summary>
/// Maps the Archive's upload report onto the wire shape.
/// </summary>
public static class UploadReportResponseMappings
{
    /// <summary>Maps an upload report onto the wire shape.</summary>
    public static UploadReportResponse ToResponse(this UploadReport report) => new(
        report.CabinetName,
        report.FieldName,
        report.FolderName,
        [.. report.Results.Select(entry => entry.ToResponse())]);
}
