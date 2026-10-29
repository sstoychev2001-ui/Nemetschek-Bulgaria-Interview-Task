using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Domain;

/// <summary>
/// One file belonging to a document, before and after it is stored. <see cref="OpenContent"/> is
/// set on the way in; <see cref="Id"/> and <see cref="SizeInBytes"/> are set by the store.
/// </summary>
public sealed record Part
{
    /// <summary>The Archive's identifier for the part; also how its content is addressed.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>The file's name as the user had it, never used as a path.</summary>
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "a stored part has no 'name'")]
    public string Name { get; init; } = "";

    /// <summary>The part's position in the document, starting at zero.</summary>
    [JsonPropertyName("order")]
    public int Order { get; init; }

    /// <summary>The length of the stored content; zero until it is stored.</summary>
    [JsonPropertyName("sizeInBytes")]
    public long SizeInBytes { get; init; }

    /// <summary>
    /// Opens the content for reading, for a part on its way into the Archive. <c>null</c> once it
    /// is stored, and on every part read back out. Never written to document.json.
    /// </summary>
    [JsonIgnore]
    public Func<Stream>? OpenContent { get; init; }

    /// <summary>A part on its way into the Archive, before the store has seen it.</summary>
    public static Part ToStore(string name, Func<Stream> openContent) =>
        new() { Name = name, OpenContent = openContent };
}
