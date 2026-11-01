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
    [ProducesResponseType<CabinetsResponse>(StatusCodes.Status200OK)]
    public CabinetsResponse Cabinets() => cabinets.All.ToResponse();
}
