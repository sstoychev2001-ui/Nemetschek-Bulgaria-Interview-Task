namespace Nemetschek.Archive.Api.Contracts.Documents.Responses;

/// <summary>
/// One page of a file cabinet's documents, as the client sees it.
/// </summary>
/// <param name="Cabinet">The file cabinet the page was read from.</param>
/// <param name="Page">The page number, starting at one.</param>
/// <param name="Size">How many documents a page holds at most.</param>
/// <param name="Total">How many documents the cabinet holds in all.</param>
/// <param name="Documents">The documents on this page, oldest first.</param>
public sealed record DocumentPageResponse(
    string Cabinet,
    int Page,
    int Size,
    int Total,
    IReadOnlyList<DocumentResponse> Documents);
