using Microsoft.AspNetCore.Mvc;

namespace Nemetschek.Export.Api.Middleware;

/// <summary>
/// Maps the refusals the Export service expects onto status codes, and answers with ProblemDetails.
/// It is the only place in the service that turns a refusal into a status, so an endpoint never
/// chooses one. Anything it does not recognise is rethrown for the exception handler, which answers
/// 500.
/// </summary>
public sealed class ExportExceptionMiddleware(
    RequestDelegate next,
    IProblemDetailsService problemDetails,
    ILogger<ExportExceptionMiddleware> logger)
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

            if (context.Response.HasStarted)
            {
                logger.LogError(
                    exception,
                    "The export failed after the response had started; the archive the caller " +
                    "received is incomplete");

                throw;
            }

            logger.LogInformation(
                "{Status} {Title}: {Reason}", refusal.Status, refusal.Title, exception.Message);

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
    /// The status and title for a refusal the Export service expects, or a rethrow when the
    /// exception is not one of them. A new expected refusal is one more arm here.
    /// </summary>
    private static Refusal Map(Exception exception) => exception switch
    {
        HttpRequestException { StatusCode: System.Net.HttpStatusCode.BadRequest or
            System.Net.HttpStatusCode.NotFound } => new Refusal(
            StatusCodes.Status400BadRequest, "The Archive refused the export"),

        HttpRequestException => new Refusal(
            StatusCodes.Status502BadGateway, "The Archive is unreachable"),
        InvalidDataException => new Refusal(
            StatusCodes.Status502BadGateway, "The Archive answered something unusable"),
        _ => throw exception,
    };

    /// <summary>How one expected refusal is answered.</summary>
    private sealed record Refusal(int Status, string Title);
}
