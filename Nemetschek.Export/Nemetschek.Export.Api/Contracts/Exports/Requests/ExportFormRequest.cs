using System.ComponentModel.DataAnnotations;

namespace Nemetschek.Export.Api.Contracts.Exports.Requests;

/// <summary>
/// The body of an export request.
/// </summary>
public sealed class ExportFormRequest : IValidatableObject
{
    /// <summary>The technical name of the file cabinet to export from.</summary>
    [Required(ErrorMessage = "Name the file cabinet to export from.")]
    public string Cabinet { get; init; } = "";

    /// <summary>The writable text field the export marker is written into.</summary>
    [Required(ErrorMessage = "Choose the index field the export marker is written into.")]
    public string MarkerField { get; init; } = "";

    /// <summary>The first calendar date to include, as the caller means it.</summary>
    [Required(ErrorMessage = "Give the date the export starts from.")]
    public DateOnly? From { get; init; }

    /// <summary>The last calendar date to include, in full, as the caller means it.</summary>
    [Required(ErrorMessage = "Give the date the export runs to.")]
    public DateOnly? To { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From is { } from && To is { } to && to < from)
        {
            yield return new ValidationResult(
                $"The to-date {to:yyyy-MM-dd} is before the from-date {from:yyyy-MM-dd}.",
                [nameof(To)]);
        }
    }
}
