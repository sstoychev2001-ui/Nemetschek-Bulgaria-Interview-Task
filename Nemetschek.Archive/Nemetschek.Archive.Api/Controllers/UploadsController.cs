using Microsoft.AspNetCore.Mvc;
using Nemetschek.Archive.Api.Contracts.Uploads.Mappings;
using Nemetschek.Archive.Api.Contracts.Uploads.Requests;
using Nemetschek.Archive.Api.Contracts.Uploads.Responses;
using Nemetschek.Archive.Core.Services.Uploads;

namespace Nemetschek.Archive.Api.Controllers;

/// <summary>
/// Uploading a folder of material into a file cabinet.
/// </summary>
[ApiController]
[Route("cabinets/{cabinetName}/uploads")]
[Produces("application/json")]
public sealed class UploadsController(UploadService uploadService) : ControllerBase
{
    /// <summary>Uploads a folder of material into a file cabinet.</summary>
    /// <param name="cabinetName">The technical name of the file cabinet, such as <c>Invoices</c>.</param>
    /// <param name="form">The chosen index field, the folder's name, and its contents.</param>
    /// <param name="cancellationToken">Cancels the upload if the caller goes away.</param>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<UploadReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<UploadReportResponse> Upload(
        string cabinetName,
        [FromForm] UploadFormRequest form,
        CancellationToken cancellationToken)
    {
        return (await uploadService.UploadAsync(
            form.ToUploadRequest(cabinetName), cancellationToken)).ToResponse();
    }
}
