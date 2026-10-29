using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Domain;

/// <summary>
/// One index field of a file cabinet, as declared in cabinets.json.
/// </summary>
public sealed record IndexField : IValidatableObject, IJsonOnDeserialized
{
    /// <summary>Enforces the rules below as part of deserialisation.</summary>
    public void OnDeserialized() =>
        Validator.ValidateObject(this, new ValidationContext(this), validateAllProperties: true);

    /// <summary>The technical name, used on the wire.</summary>
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "an index field has no 'name'")]
    public string Name { get; init; } = "";

    /// <summary>The display label, shown by the client.</summary>
    [JsonPropertyName("label")]
    [Required(ErrorMessage = "an index field has no 'label'")]
    public string Label { get; init; } = "";

    /// <summary>The field's type.</summary>
    [JsonPropertyName("type")]
    public required FieldType Type { get; init; }

    /// <summary>
    /// The maximum length of a value in this field, whatever its type; <c>null</c> for unbounded.
    /// </summary>
    [JsonPropertyName("maxLength")]
    public int? MaxLength { get; init; }

    /// <summary>Whether a client may set this field.</summary>
    [JsonPropertyName("writable")]
    public bool Writable { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        if (MaxLength is <= 0)
        {
            results.Add(new ValidationResult(
                $"index field '{Name}' declares maxLength {MaxLength}; " +
                "a field must allow at least one character"));
        }

        return results;
    }
}
