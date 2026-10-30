using System.Text.Json.Serialization;
using Nemetschek.Export.Core.Domain.Exports;

namespace Nemetschek.Export.Infrastructure.Archive;

/// <summary>
/// One part of one of the Archive's documents, as its HTTP API describes it.
/// </summary>
internal sealed class ArchivePartDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// The domain part this describes.
    /// </summary>
    /// <exception cref="InvalidDataException">The Archive answered without an id or a name.</exception>
    public ArchivePart ToPart() =>
        Id == Guid.Empty || string.IsNullOrEmpty(Name)
            ? throw new InvalidDataException(
                $"The Archive returned a part with no id or no name: '{Id}', '{Name}'.")
            : new ArchivePart(Id, Name);
}
