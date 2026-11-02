using System.Text.Json.Serialization;

namespace Nemetschek.Export.Core.Domain.Exports;

/// <summary>
/// One document: the shape the Archive's HTTP API describes and the shape the export rules use. One
/// type, as with the Archive's own <c>Document</c>, so nothing is copied between two.
/// </summary>
/// <param name="Id">The Archive's identifier.</param>
/// <param name="Title">The document's title, which the zip entry is named after.</param>
/// <param name="Parts">Its parts, in the order the Archive stored them.</param>
public sealed record ArchiveDocument(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("parts")] IReadOnlyList<ArchivePart> Parts) : IJsonOnDeserialized
{
    /// <summary>Refuses a document the export rules could not name a zip entry after.</summary>
    /// <exception cref="InvalidDataException">
    /// The Archive answered without an id, without a title, or without a parts list.
    /// </exception>
    public void OnDeserialized()
    {
        if (Id == Guid.Empty || string.IsNullOrEmpty(Title) || Parts is null)
        {
            throw new InvalidDataException(
                $"The Archive returned a document with no id or no title: '{Id}', '{Title}'.");
        }
    }
}
