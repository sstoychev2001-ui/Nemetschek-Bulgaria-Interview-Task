using Microsoft.AspNetCore.Mvc;
using Nemetschek.Archive.Api.Contracts.Documents.Mappings;
using Nemetschek.Archive.Api.Contracts.Documents.Requests;
using Nemetschek.Archive.Api.Contracts.Documents.Responses;
using Nemetschek.Archive.Api.Contracts.Shared;
using Nemetschek.Archive.Api.Contracts.Uploads.Mappings;
using Nemetschek.Archive.Api.Contracts.Uploads.Requests;
using Nemetschek.Archive.Api.Contracts.Uploads.Responses;
using Nemetschek.Archive.Core.Services.Documents;
using Nemetschek.Archive.Core.Services.Uploads;

namespace Nemetschek.Archive.Api.Controllers;

/// <summary>
/// The file cabinets and what goes into them.
/// </summary>
[ApiController]
[Route("cabinets")]
[Produces("application/json")]
public sealed class CabinetsController(
    UploadService uploadService,
    DocumentsService documentsService,
    PagingLimits paging) : ControllerBase
{
    /// <summary>Uploads a folder of material into a file cabinet.</summary>
    /// <param name="cabinetName">The technical name of the file cabinet, such as <c>Invoices</c>.</param>
    /// <param name="form">The chosen index field, the folder's name, and its contents.</param>
    /// <param name="cancellationToken">Cancels the upload if the caller goes away.</param>
    [HttpPost("{cabinetName}/uploads")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<UploadReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<UploadReportResponse> Upload(
        string cabinetName,
        [FromForm] UploadFormRequest form,
        CancellationToken cancellationToken)
    {
        var report = await uploadService.UploadAsync(
            form.ToUploadRequest(cabinetName), cancellationToken);

        return report.ToResponse();
    }

    /// <summary>Reads a page of the documents stored in a file cabinet, oldest first.</summary>
    /// <param name="cabinetName">The technical name of the file cabinet, such as <c>Invoices</c>.</param>
    /// <param name="query">Which page to read, and how large it should be.</param>
    /// <param name="cancellationToken">Cancels the read if the caller goes away.</param>
    [HttpGet("{cabinetName}/documents")]
    [ProducesResponseType<DocumentPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<DocumentPageResponse> Documents(
        string cabinetName,
        [FromQuery] DocumentPageRequest query,
        CancellationToken cancellationToken)
    {
        var page = await documentsService.ListAsync(
            cabinetName,
            query.Skip(paging.MaxPageSize),
            query.Take(paging.MaxPageSize),
            cancellationToken);

        return page.ToResponse(cabinetName, query.Page, query.Take(paging.MaxPageSize));
    }
}
