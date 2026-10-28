using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Domain.Cabinets;

/// <summary>
/// A file cabinet: a fixed set of index fields shared by every document it holds.
/// </summary>
public sealed class Cabinet : IValidatableObject, IJsonOnDeserialized
{
    /// <summary>Enforces the rules below as part of deserialization.</summary>
    public void OnDeserialized() =>
        Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);

    /// <summary>The technical name, used on the wire.</summary>
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "a file cabinet has no 'name'")]
    public string Name { get; init; } = "";

    /// <summary>The display name, shown by the client.</summary>
    [JsonPropertyName("displayName")]
    [Required(ErrorMessage = "a file cabinet has no 'displayName'")]
    public string DisplayName { get; init; } = "";

    /// <summary>The index fields, in the order they are declared in cabinets.json.</summary>
    [JsonPropertyName("fields")]
    public IReadOnlyList<IndexField> Fields { get; init; } = [];

    /// <summary>
    /// The fields a client may choose as the upload field or the export marker field.
    /// </summary>
    // Both the upload rules and the export rules require a writable text field, so this is the set a
    // client may offer the user to choose from.
    [JsonIgnore]
    public IEnumerable<IndexField> WritableTextFields =>
        Fields.Where(field => field.Writable && field.Type == FieldType.Text);

    /// <summary>
    /// Finds an index field by technical name, case-insensitively. Returns <c>null</c> if this
    /// cabinet has no such field.
    /// </summary>
    public IndexField? FindField(string fieldName) =>
        Fields.FirstOrDefault(field => string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        if (Fields.Count == 0)
        {
            results.Add(new ValidationResult($"file cabinet '{Name}' declares no 'fields'"));
        }

        var duplicates = Fields
            .GroupBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        results.AddRange(duplicates.Select(duplicate =>
            new ValidationResult($"Index field name {duplicate.Key} is not unique in cabinet {Name}.")));

        return results;
    }
}
