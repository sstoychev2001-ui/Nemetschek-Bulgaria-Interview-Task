using System.ComponentModel.DataAnnotations;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Api.Contracts.Documents.Requests;

/// <summary>
/// One index value on the way in: the type it claims to be, and its value as text.
/// </summary>
public sealed class IndexValueRequest : IValidatableObject
{
    /// <summary>Whether the value is text, a number or a date.</summary>
    public required FieldType Type { get; init; }

    /// <summary>The value, as text. A date is <c>yyyy-MM-dd</c>.</summary>
    [Required(ErrorMessage = "an index value has no 'value'")]
    public string Value { get; init; } = "";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ToIndexValue() is null)
        {
            yield return new ValidationResult(
                $"'{Value}' is not a usable {Type.ToString().ToLowerInvariant()} value.",
                [nameof(Value)]);
        }
    }

    /// <summary>
    /// The domain value this describes, or <c>null</c> when the text does not parse as its type.
    /// </summary>
    public IndexValue? ToIndexValue() => Type switch
    {
        FieldType.Text => new IndexValue.Text(Value),
        FieldType.Number => decimal.TryParse(
            Value, System.Globalization.CultureInfo.InvariantCulture, out var number)
            ? new IndexValue.Number(number)
            : null,
        FieldType.Date => DateOnly.TryParseExact(Value, "yyyy-MM-dd", out var date)
            ? new IndexValue.Date(date)
            : null,
        _ => null,
    };
}
