using Microsoft.AspNetCore.Mvc;
using Nemetschek.Archive.Api.Contracts.Documents.Mappings;
using Nemetschek.Archive.Api.Contracts.Documents.Requests;
using Nemetschek.Archive.Api.Contracts.Documents.Responses;
using Nemetschek.Archive.Api.Contracts.Shared;
using Nemetschek.Archive.Core.Services.Documents;
using Nemetschek.Archive.Core.Validation.Documents;

namespace Nemetschek.Archive.Api.Controllers;

/// <summary>
/// The documents of one file cabinet: reading them, reading a part's content, and writing index values back.
/// </summary>
[ApiController]
[Route("cabinets/{cabinetName}/documents")]
[Produces("application/json")]
public sealed class DocumentsController(
    DocumentsService documentsService,
    PagingLimits paging) : ControllerBase
{
    /// <summary>Reads a page of the documents stored in a file cabinet, oldest first.</summary>
    /// <param name="cabinetName">The technical name of the file cabinet, such as <c>Invoices</c>.</param>
    /// <param name="query">Which page to read, how large it should be, and over which dates.</param>
    /// <param name="cancellationToken">Cancels the read if the caller goes away.</param>
    [HttpGet]
    [ProducesResponseType<DocumentPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<DocumentPageResponse> Documents(
        string cabinetName,
        [FromQuery] DocumentPageRequest query,
        CancellationToken cancellationToken)
    {
        var page = await documentsService.ListAsync(
            cabinetName,
            CalendarRange.Of(query.From, query.To),
            query.Skip(paging.MaxPageSize),
            query.Take(paging.MaxPageSize),
            cancellationToken);

        return page.ToResponse(cabinetName, query.Page, query.Take(paging.MaxPageSize));
    }

    /// <summary>Reads the content of one part of one stored document.</summary>
    /// <param name="cabinetName">The technical name of the file cabinet.</param>
    /// <param name="documentId">The Archive's identifier for the document.</param>
    /// <param name="partId">The Archive's identifier for the part.</param>
    /// <param name="cancellationToken">Cancels the read if the caller goes away.</param>
    [HttpGet("{documentId:guid}/parts/{partId:guid}/content")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<FileStreamResult> PartContent(
        string cabinetName,
        Guid documentId,
        Guid partId,
        CancellationToken cancellationToken)
    {
        var content = await documentsService.OpenPartAsync(
            cabinetName, documentId, partId, cancellationToken);

        return File(content, "application/octet-stream");
    }

    /// <summary>Sets the same index values on several stored documents of one file cabinet.</summary>
    /// <param name="cabinetName">The technical name of the file cabinet.</param>
    /// <param name="request">The documents to change, and the values to set on each of them.</param>
    /// <param name="cancellationToken">Cancels the write if the caller goes away.</param>
    [HttpPatch("index-values")]
    [ProducesResponseType<IndexValuesUpdatedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IndexValuesUpdatedResponse> SetIndexValues(
        string cabinetName,
        [FromBody] IndexValuesBatchRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await documentsService.SetIndexValuesAsync(
            cabinetName, request.DocumentIds, request.ToIndexValues(), cancellationToken);

        return new IndexValuesUpdatedResponse(cabinetName, updated);
    }
}
