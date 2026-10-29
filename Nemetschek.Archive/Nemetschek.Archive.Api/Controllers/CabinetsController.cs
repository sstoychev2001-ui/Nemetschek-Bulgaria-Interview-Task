using Microsoft.AspNetCore.Mvc;
using Nemetschek.Archive.Api.Contracts.Documents;
using Nemetschek.Archive.Api.Contracts.Uploads;
using Nemetschek.Archive.Api.Results;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.Uploads;

namespace Nemetschek.Archive.Api.Controllers;

/// <summary>
/// The file cabinets and what goes into them.
/// </summary>
[ApiController]
[Route("cabinets")]
[Produces("application/json")]
public sealed class CabinetsController(
    UploadService uploadService,
    DocumentQueryService documentQueryService,
    PagingLimits paging) : ControllerBase
{
    /// <summary>Uploads a folder of material into a file cabinet.</summary>
    /// <param name="cabinetName">The technical name of the file cabinet, such as <c>Invoices</c>.</param>
    /// <param name="form">The chosen index field, the folder's name, and its contents.</param>
    /// <param name="cancellationToken">Cancels the upload if the caller goes away.</param>
    [HttpPost("{cabinetName}/uploads")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<UploadReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<UploadReportResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Upload(
        string cabinetName,
        [FromForm] UploadFormRequest form,
        CancellationToken cancellationToken)
    {
        var report = await uploadService.UploadAsync(
            form.ToUploadRequest(cabinetName), cancellationToken);

        return this.UploadReport(report);
    }

    /// <summary>Reads a page of the documents stored in a file cabinet, oldest first.</summary>
    /// <param name="cabinetName">The technical name of the file cabinet, such as <c>Invoices</c>.</param>
    /// <param name="query">Which page to read, and how large it should be.</param>
    /// <param name="cancellationToken">Cancels the read if the caller goes away.</param>
    [HttpGet("{cabinetName}/documents")]
    [ProducesResponseType<DocumentPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Documents(
        string cabinetName,
        [FromQuery] DocumentPageRequest query,
        CancellationToken cancellationToken)
    {
        var page = await documentQueryService.ListAsync(
            cabinetName,
            query.Skip(paging.MaxPageSize),
            query.Take(paging.MaxPageSize),
            cancellationToken);

        return this.DocumentPageResult(cabinetName, query, paging.MaxPageSize, page);
    }
}
