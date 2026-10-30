using System.Text.Json.Serialization;
using Nemetschek.Export.Core.Domain.Exports;

namespace Nemetschek.Export.Infrastructure.Archive;

/// <summary>
/// One of the Archive's documents, as its HTTP API describes it.
/// </summary>
internal sealed class ArchiveDocumentDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("parts")]
    public List<ArchivePartDto> Parts { get; init; } = [];

    /// <summary>
    /// The domain document this describes.
    /// </summary>
    /// <exception cref="InvalidDataException">The Archive answered without an id or a title.</exception>
    public ArchiveDocument ToDocument() =>
        Id == Guid.Empty || string.IsNullOrEmpty(Title)
            ? throw new InvalidDataException(
                $"The Archive returned a document with no id or no title: '{Id}', '{Title}'.")
            : new ArchiveDocument(Id, Title, [.. Parts.Select(part => part.ToPart())]);
}
