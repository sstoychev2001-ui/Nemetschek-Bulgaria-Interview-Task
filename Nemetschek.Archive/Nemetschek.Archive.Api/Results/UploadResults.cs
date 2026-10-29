using Microsoft.AspNetCore.Mvc;
using Nemetschek.Archive.Api.Contracts.Uploads;
using Nemetschek.Archive.Api.Middleware;
using Nemetschek.Archive.Core.Domain.Uploads;

namespace Nemetschek.Archive.Api.Results;

/// <summary>
/// Turns an upload report into its HTTP response.
/// </summary>
public static class UploadResults
{
    /// <summary>Answers with the upload's report, and hands it to the pipeline to judge.</summary>
    public static IActionResult UploadReport(this ControllerBase controller, UploadReport report)
    {
        UploadFailureStatusMiddleware.RecordUploadReport(controller.HttpContext, report);

        return controller.Ok(UploadReportResponse.From(report));
    }
}
