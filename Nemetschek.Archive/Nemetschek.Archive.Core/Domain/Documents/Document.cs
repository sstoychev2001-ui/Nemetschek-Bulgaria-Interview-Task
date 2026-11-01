using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Domain.Documents;

/// <summary>
/// One record in a file cabinet: its index values plus zero or more parts, before and after it is
/// stored. This is also the shape written to document.json. The system fields are set by the store
/// and ignored on the way in.
/// </summary>
public sealed record Document : IValidatableObject, IJsonOnDeserialized
{
    /// <summary>Enforces the rules below as part of deserialisation.</summary>
    public void OnDeserialized() =>
        Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);

    /// <summary>The Archive's identifier; <see cref="Guid.Empty"/> until it is stored.</summary>
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    /// <summary>The technical name of the file cabinet holding it.</summary>
    [JsonPropertyName("cabinet")]
    [Required(ErrorMessage = "a stored document has no 'cabinet'")]
    public string CabinetName { get; init; } = "";

    /// <summary>The name of the uploaded file or folder it came from.</summary>
    [JsonPropertyName("title")]
    [Required(ErrorMessage = "a stored document has no 'title'")]
    public string Title { get; init; } = "";

    /// <summary>The index values, keyed by the field's technical name.</summary>
    [JsonPropertyName("indexValues")]
    public IReadOnlyDictionary<string, IndexValue> IndexValues { get; init; } =
        new Dictionary<string, IndexValue>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The parts, in the order they were stored.</summary>
    [JsonPropertyName("parts")]
    public IReadOnlyList<Part> Parts { get; init; } = [];

    /// <summary>When the Archive first stored it, in UTC; default until it is stored.</summary>
    [JsonPropertyName("storedAt")]
    public DateTimeOffset StoredAt { get; init; }

    /// <summary>When the Archive last changed it, in UTC.</summary>
    [JsonPropertyName("modifiedAt")]
    public DateTimeOffset ModifiedAt { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        if (Id == Guid.Empty)
        {
            results.Add(new ValidationResult("a stored document has no 'id'"));
        }

        foreach (var part in Parts)
        {
            Validator.ValidateObject(part, new ValidationContext(part), validateAllProperties: true);
        }

        return results;
    }
}
