using Microsoft.AspNetCore.Mvc;
using Nemetschek.Archive.Core.Exceptions.Cabinets;
using Nemetschek.Archive.Core.Exceptions.Documents;
using Nemetschek.Archive.Core.Exceptions.Uploads;

namespace Nemetschek.Archive.Api.Middleware;

/// <summary>
/// Maps the refusals the Archive expects onto status codes, and answers with ProblemDetails. It is
/// the only place in the service that turns a refusal into a status, so an endpoint never chooses
/// one. Anything it does not recognise is rethrown for the exception handler, which answers 500.
/// </summary>
public sealed class ArchiveExceptionMiddleware(
    RequestDelegate next,
    IProblemDetailsService problemDetails,
    ILogger<ArchiveExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var refusal = Map(exception);
            logger.LogInformation("{Status} {Title}: {Reason}", refusal.Status, refusal.Title, exception.Message);

            context.Response.StatusCode = refusal.Status;

            await problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Title = refusal.Title,
                    Detail = exception.Message,
                    Status = refusal.Status,
                },
            });
        }
    }

    /// <summary>
    /// The status and title for a refusal the Archive expects, or <c>null</c> when the exception
    /// is not one of them. A new expected refusal is one more arm here.
    /// </summary>
    private static Refusal Map(Exception exception) => exception switch
    {
        UnknownCabinetException => new Refusal(StatusCodes.Status404NotFound, "Unknown file cabinet"),
        DocumentNotFoundException => new Refusal(StatusCodes.Status404NotFound, "Unknown document"),
        UploadRefusedException => new Refusal(StatusCodes.Status400BadRequest, "The upload was refused"),
        IndexValueRefusedException => new Refusal(StatusCodes.Status400BadRequest, "The index value was refused"),
        InvalidDateRangeException => new Refusal(StatusCodes.Status400BadRequest, "The date range is not usable"),
        _ => throw exception
    };

    /// <summary>How one expected refusal is answered.</summary>
    private sealed record Refusal(int Status, string Title);
}
