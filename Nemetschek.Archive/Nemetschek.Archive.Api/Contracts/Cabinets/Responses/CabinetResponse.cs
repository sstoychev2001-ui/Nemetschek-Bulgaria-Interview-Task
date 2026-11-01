namespace Nemetschek.Archive.Api.Contracts.Cabinets.Responses;

/// <summary>
/// One file cabinet and its index fields, as the client sees it.
/// </summary>
/// <param name="Name">The technical name, used in every other route.</param>
/// <param name="DisplayName">The name to show the user.</param>
/// <param name="Fields">Its index fields, in the order cabinets.json declares them.</param>
public sealed record CabinetResponse(
    string Name,
    string DisplayName,
    IReadOnlyList<IndexFieldResponse> Fields);
