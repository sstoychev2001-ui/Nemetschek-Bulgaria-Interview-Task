using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Infrastructure.Documents;

/// <summary>
/// A document's metadata, as written to document.json beside its parts.
/// </summary>
internal sealed class DocumentFile : IValidatableObject, IJsonOnDeserialized
{
    /// <summary>Enforces the rules below as part of deserialisation.</summary>
    public void OnDeserialized() =>
        Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);

    /// <summary>The Archive's identifier for the document.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>The technical name of the file cabinet holding it.</summary>
    [JsonPropertyName("cabinet")]
    [Required(ErrorMessage = "a stored document has no 'cabinet'")]
    public string Cabinet { get; init; } = "";

    /// <summary>The name of the uploaded file or folder it came from.</summary>
    [JsonPropertyName("title")]
    [Required(ErrorMessage = "a stored document has no 'title'")]
    public string Title { get; init; } = "";

    /// <summary>When the Archive first stored it, in UTC.</summary>
    [JsonPropertyName("storedAt")]
    public DateTimeOffset StoredAt { get; init; }

    /// <summary>When the Archive last changed it, in UTC.</summary>
    [JsonPropertyName("modifiedAt")]
    public DateTimeOffset ModifiedAt { get; init; }

    /// <summary>The index values, keyed by the field's technical name.</summary>
    [JsonPropertyName("indexValues")]
    public Dictionary<string, IndexValueFile> IndexValues { get; init; } = [];

    /// <summary>The parts, in the order they were stored.</summary>
    [JsonPropertyName("parts")]
    public List<PartFile> Parts { get; init; } = [];

    /// <summary>Maps a stored document onto the domain.</summary>
    public Document ToDocument() => new(
        Id,
        Cabinet,
        Title,
        IndexValues.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToIndexValue(),
            StringComparer.OrdinalIgnoreCase),
        [.. Parts.OrderBy(part => part.Order).Select(part => part.ToPart())],
        StoredAt,
        ModifiedAt);

    /// <summary>Maps a domain document onto the stored shape.</summary>
    public static DocumentFile From(Document document) => new()
    {
        Id = document.Id,
        Cabinet = document.CabinetName,
        Title = document.Title,
        StoredAt = document.StoredAt,
        ModifiedAt = document.ModifiedAt,
        IndexValues = document.IndexValues.ToDictionary(
            entry => entry.Key,
            entry => IndexValueFile.From(entry.Value),
            StringComparer.OrdinalIgnoreCase),
        Parts = [.. document.Parts.Select(PartFile.From)],
    };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        if (Id == Guid.Empty)
        {
            results.Add(new ValidationResult("a stored document has no 'id'"));
        }

        // Validator does not walk into nested objects, so the parts validate themselves here.
        foreach (var part in Parts)
        {
            Validator.ValidateObject(part, new ValidationContext(part), validateAllProperties: true);
        }

        return results;
    }
}
