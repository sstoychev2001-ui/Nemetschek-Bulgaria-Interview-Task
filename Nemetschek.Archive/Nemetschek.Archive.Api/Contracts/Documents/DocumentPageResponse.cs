using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Api.Contracts.Documents;

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
    IReadOnlyList<DocumentResponse> Documents)
{
    /// <summary>Maps a page of documents onto the wire shape.</summary>
    public static DocumentPageResponse From(string cabinet, int page, int size, DocumentPage read) =>
        new(cabinet, page, size, read.Total, [.. read.Documents.Select(DocumentResponse.From)]);
}
