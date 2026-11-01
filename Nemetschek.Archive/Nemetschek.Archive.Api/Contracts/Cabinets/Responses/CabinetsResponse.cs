namespace Nemetschek.Archive.Api.Contracts.Cabinets.Responses;

/// <summary>
/// Every file cabinet the Archive was deployed with, as the client sees it.
/// </summary>
/// <param name="Cabinets">The cabinets, in the order cabinets.json declares them.</param>
public sealed record CabinetsResponse(IReadOnlyList<CabinetResponse> Cabinets);
