using Nemetschek.Archive.Api.Contracts.Documents.Responses;
using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Api.Contracts.Documents.Mappings;

/// <summary>
/// Maps a stored part onto the wire shape.
/// </summary>
public static class PartResponseMappings
{
    /// <summary>Maps a stored part onto the wire shape.</summary>
    public static PartResponse ToResponse(this Part part) =>
        new(part.Id, part.Name, part.Order, part.SizeInBytes);
}
