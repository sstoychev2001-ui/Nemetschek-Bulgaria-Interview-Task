using Nemetschek.Archive.Core.Domain.Uploads;

namespace Nemetschek.Archive.Api.Middleware;

/// <summary>
/// Logs anything an upload failed to store, and raises the response status to 404 when there was
/// at least one such failure.
/// </summary>
// The one place in the service that turns an outcome into a status code, and the one place a
// failure is written to the log. Endpoints answer 200 with their report and hold neither concern:
// no status logic and no logger. When the code or the condition changes, this is the file.
//
// It hooks Response.OnStarting rather than inspecting the body, because by the time MVC has
// serialised a response the status line is already on its way. OnStarting runs after the action has
// recorded its report and before the first byte is written, which is the only moment both facts are
// available. The alternative — buffering the response into a MemoryStream and parsing the JSON back
// out to look for failures — would cost the streaming the rest of the upload path is built for.
public sealed class UploadFailureStatusMiddleware(
    RequestDelegate next,
    ILogger<UploadFailureStatusMiddleware> logger)
{
    private const string ReportKey = "Archive.UploadReport";

    /// <summary>
    /// Records the report of the upload this request ran, for the middleware to judge.
    /// </summary>
    public static void RecordUploadReport(HttpContext context, UploadReport report) =>
        context.Items[ReportKey] = report;

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (context.Items[ReportKey] is UploadReport report)
            {
                Judge(report, context);
            }

            return Task.CompletedTask;
        });

        await next(context);
    }

    private void Judge(UploadReport report, HttpContext context)
    {
        var failures = report.Results
            .Where(result => result.Outcome == UploadEntryOutcome.Failed)
            .ToList();

        foreach (var failure in failures)
        {
            // The rules cannot log — Core has no dependencies — so this is the only place a failure
            // becomes visible to an operator. It matters: a caller that only reads the status sees
            // one 404 whether one document failed or the whole upload was refused.
            logger.LogWarning(
                "Upload into cabinet {Cabinet}: entry {Entry} was not stored. {Reason}",
                report.CabinetName, failure.Entry, failure.Reason);
        }

        if (failures.Count > 0)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
        }
    }
}
