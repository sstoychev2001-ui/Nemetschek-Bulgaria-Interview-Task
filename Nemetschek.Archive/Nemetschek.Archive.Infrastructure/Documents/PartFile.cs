using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Infrastructure.Documents;

/// <summary>
/// One part as it is written to document.json.
/// </summary>
internal sealed class PartFile
{
    /// <summary>The folder a document's part content sits in.</summary>
    public const string PartsFolder = "parts";

    /// <summary>The Archive's identifier for the part.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>The file's name as the user had it.</summary>
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "a stored part has no 'name'")]
    public string Name { get; init; } = "";

    /// <summary>The part's position in the document, starting at zero.</summary>
    [JsonPropertyName("order")]
    public int Order { get; init; }

    /// <summary>The length of the stored content.</summary>
    [JsonPropertyName("sizeInBytes")]
    public long SizeInBytes { get; init; }

    /// <summary>Where the content sits, relative to the document's own directory.</summary>
    [JsonPropertyName("file")]
    [Required(ErrorMessage = "a stored part has no 'file'")]
    public string File { get; init; } = "";

    /// <summary>Where a part's content sits, relative to its document's directory.</summary>
    public static string PathFor(Part part) => $"{PartsFolder}/{part.Order:D3}_{part.Id}.bin";

    /// <summary>Maps a stored part onto the domain.</summary>
    public Part ToPart() => new(Id, Name, Order, SizeInBytes);

    /// <summary>Maps a domain part onto the stored shape.</summary>
    public static PartFile From(Part part) => new()
    {
        Id = part.Id,
        Name = part.Name,
        Order = part.Order,
        SizeInBytes = part.SizeInBytes,
        File = PathFor(part),
    };
}
