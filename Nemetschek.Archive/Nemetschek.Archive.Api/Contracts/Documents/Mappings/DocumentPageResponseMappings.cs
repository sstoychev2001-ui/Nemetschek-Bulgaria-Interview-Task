using Nemetschek.Archive.Api.Contracts.Documents.Responses;
using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Api.Contracts.Documents.Mappings;

/// <summary>
/// Maps a page of documents onto the wire shape.
/// </summary>
public static class DocumentPageResponseMappings
{
    /// <summary>Maps a page of documents onto the wire shape.</summary>
    public static DocumentPageResponse ToResponse(
        this DocumentPage page,
        string cabinet,
        int number,
        int size) =>
        new(cabinet, number, size, page.Total,
            [.. page.Documents.Select(document => document.ToResponse())]);
}
