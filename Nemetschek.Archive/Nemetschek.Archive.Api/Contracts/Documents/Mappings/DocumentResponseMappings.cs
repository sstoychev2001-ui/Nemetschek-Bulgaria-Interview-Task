using Nemetschek.Archive.Api.Contracts.Documents.Responses;
using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Api.Contracts.Documents.Mappings;

/// <summary>
/// Maps a stored document onto the wire shape.
/// </summary>
public static class DocumentResponseMappings
{
    /// <summary>Maps a stored document onto the wire shape.</summary>
    public static DocumentResponse ToResponse(this Document document) => new(
        document.Id,
        document.Title,
        document.IndexValues.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToResponse(),
            StringComparer.OrdinalIgnoreCase),
        [.. document.Parts.Select(part => part.ToResponse())],
        document.StoredAt,
        document.ModifiedAt);
}
