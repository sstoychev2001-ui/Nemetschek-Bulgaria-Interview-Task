using System.Text.Json.Serialization;

namespace Nemetschek.Export.Core.Domain.Exports;

/// <summary>
/// One part of a document: the shape the Archive's HTTP API describes and the shape the export rules
/// use. One type, as with the Archive's own <c>Document</c>, so nothing is copied between two.
/// </summary>
/// <param name="Id">The Archive's identifier, which the content is fetched by.</param>
/// <param name="Name">The file's name as the user had it.</param>
public sealed record ArchivePart(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name) : IJsonOnDeserialized
{
    /// <summary>Refuses a part the export rules could not name a zip entry after.</summary>
    /// <exception cref="InvalidDataException">The Archive answered without an id or a name.</exception>
    public void OnDeserialized()
    {
        if (Id == Guid.Empty || string.IsNullOrEmpty(Name))
        {
            throw new InvalidDataException($"The Archive returned a part with no id or no name: '{Id}', '{Name}'.");
        }
    }
}
