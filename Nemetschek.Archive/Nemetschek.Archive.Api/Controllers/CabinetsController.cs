using Microsoft.AspNetCore.Mvc;
using Nemetschek.Archive.Api.Contracts.Cabinets.Mappings;
using Nemetschek.Archive.Api.Contracts.Cabinets.Responses;
using Nemetschek.Archive.Core.Services.Interfaces.Cabinets;

namespace Nemetschek.Archive.Api.Controllers;

/// <summary>
/// The file cabinets the Archive was deployed with, and the index fields of each.
/// </summary>
[ApiController]
[Route("cabinets")]
[Produces("application/json")]
public sealed class CabinetsController(ICabinetCatalog cabinets) : ControllerBase
{
    /// <summary>Lists every file cabinet with its index fields.</summary>
    [HttpGet]
    [EndpointSummary("List the file cabinets")]
    [EndpointDescription(
        "Every file cabinet the Archive was deployed with, each with its index fields. A field " +
        "reports its type, its maximum length, whether a client may write it, and whether it may " +
        "be chosen as an upload field or an export marker field.")]
    [ProducesResponseType<CabinetsResponse>(StatusCodes.Status200OK)]
    public CabinetsResponse Cabinets() => cabinets.All.ToResponse();
}
