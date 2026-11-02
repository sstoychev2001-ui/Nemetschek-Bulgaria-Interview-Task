using Microsoft.AspNetCore.Mvc;
using Nemetschek.Export.Api.Contracts.Exports.Mappings;
using Nemetschek.Export.Api.Contracts.Exports.Requests;
using Nemetschek.Export.Core.Services.Exports;

namespace Nemetschek.Export.Api.Controllers;

/// <summary>
/// Exports documents out of a file cabinet as a zip archive.
/// </summary>
[ApiController]
[Route("exports")]
public sealed class ExportsController(ExportService exportService) : ControllerBase
{
    /// <summary>Builds a zip archive of a cabinet's documents and sends it back.</summary>
    /// <param name="form">The cabinet, the marker field, and the dates to cover.</param>
    /// <param name="cancellationToken">Cancels the export if the caller goes away.</param>
    [HttpPost]
    [Produces("application/zip")]
    [EndpointSummary("Export documents as a zip")]
    [EndpointDescription(
        "Builds a zip of the file cabinet's documents stored between from and to inclusive, then " +
        "marks every exported document in the chosen field. A document of one part becomes a file " +
        "named after the document.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<FileStreamResult> Export(
        [FromBody] ExportFormRequest form,
        CancellationToken cancellationToken)
    {
        var archive = await exportService.WriteAsync(form.ToExportRequest(), cancellationToken);

        return File(archive, "application/zip", form.ToFileName());
    }
}
