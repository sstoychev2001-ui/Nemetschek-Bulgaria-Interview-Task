using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Api.Contracts.Documents;

/// <summary>
/// One part of a stored document, as the client sees it.
/// </summary>
/// <param name="Id">The Archive's identifier for the part.</param>
/// <param name="Name">The file's name as the user had it.</param>
/// <param name="Order">The part's position in the document, starting at zero.</param>
/// <param name="SizeInBytes">The length of the stored content.</param>
public sealed record PartResponse(Guid Id, string Name, int Order, long SizeInBytes)
{
    /// <summary>Maps a stored part onto the wire shape.</summary>
    public static PartResponse From(Part part) =>
        new(part.Id, part.Name, part.Order, part.SizeInBytes);
}
