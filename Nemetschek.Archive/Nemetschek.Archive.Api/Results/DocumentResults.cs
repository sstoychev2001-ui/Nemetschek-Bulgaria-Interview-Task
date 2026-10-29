using Microsoft.AspNetCore.Mvc;
using Nemetschek.Archive.Api.Contracts.Documents;
using DomainDocumentPage = Nemetschek.Archive.Core.Domain.Documents.DocumentPage;

namespace Nemetschek.Archive.Api.Results;

/// <summary>
/// Turns a page of documents into its HTTP response.
/// </summary>
// An extension on ControllerBase rather than a private method on the controller, so the controller
// holds nothing but its actions.
public static class DocumentResults
{
    /// <summary>Answers with a page of documents, or says the file cabinet does not exist.</summary>
    // Unlike the upload endpoint, a read has no report to put a refusal in, so an unknown cabinet
    // is ProblemDetails here. That inconsistency between the two endpoints is deliberate for now
    // and recorded in DECISIONS.md.
    public static IActionResult DocumentPageResult(
        this ControllerBase controller,
        string cabinetName,
        DocumentPageRequest query,
        int maxPageSize,
        DomainDocumentPage? page) =>
        page is null
            ? controller.Problem(
                title: "Unknown file cabinet",
                detail: $"There is no file cabinet named '{cabinetName}'.",
                statusCode: StatusCodes.Status404NotFound)
            : controller.Ok(DocumentPageResponse.From(
                cabinetName, query.Page, query.Take(maxPageSize), page));
}
