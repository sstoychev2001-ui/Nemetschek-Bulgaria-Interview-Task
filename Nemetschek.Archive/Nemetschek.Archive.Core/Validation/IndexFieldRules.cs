using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Validation;

/// <summary>
/// The index field rules of the Archive, in one place.
/// </summary>
public static class IndexFieldRules
{
    private static readonly IReadOnlyDictionary<FieldType, IIndexValueValidator> ValueValidators =
        new IIndexValueValidator[]
            {
                new TextValueValidator(), new NumberValueValidator(), new DateValueValidator(),
            }
            .ToDictionary(validator => validator.Type);

    /// <summary>
    /// Checks a write of <paramref name="value"/> into an index field. Returns <c>null</c> when the
    /// write is allowed, otherwise the reason it was refused.
    /// </summary>
    /// <exception cref="NotSupportedException">The field's type has no validator registered.</exception>
    public static FieldViolation? ValidateWrite(Cabinet cabinet, string fieldName, IndexValue value)
    {
        var field = FindField(cabinet, fieldName);
        if (field is null)
        {
            return new FieldViolation(
                fieldName,
                $"File cabinet '{cabinet.Name}' has no index field '{fieldName}'.");
        }

        if (!field.Writable)
        {
            return new FieldViolation(
                field.Name,
                $"Index field '{field.Name}' is read-only and cannot be set by a client.");
        }

        if (field.Type != value.Type)
        {
            return new FieldViolation(
                field.Name,
                $"Index field '{field.Name}' holds a {Describe(field.Type)} value, " +
                $"but a {Describe(value.Type)} value was supplied.");
        }

        return ValidatorFor(field.Type).Validate(field, value);
    }

    /// <summary>
    /// Checks that a field may be chosen as the upload field or the export marker field. Returns
    /// <c>null</c> when the choice is allowed, otherwise the reason it was refused.
    /// </summary>
    public static FieldViolation? ValidateChosenTextField(Cabinet cabinet, string fieldName)
    {
        var field = FindField(cabinet, fieldName);
        if (field is null)
        {
            return new FieldViolation(
                fieldName,
                $"File cabinet '{cabinet.Name}' has no index field '{fieldName}'.");
        }

        if (!field.Writable)
        {
            return new FieldViolation(
                field.Name,
                $"Index field '{field.Name}' is read-only and cannot be chosen.");
        }

        if (field.Type != FieldType.Text)
        {
            return new FieldViolation(
                field.Name,
                $"Index field '{field.Name}' holds a {Describe(field.Type)} value; " +
                "a text field is required.");
        }

        return null;
    }

    /// <summary>
    /// Finds an index field by technical name. Returns <c>null</c> if the cabinet has no such field.
    /// </summary>
    private static IndexField? FindField(Cabinet cabinet, string fieldName) =>
        cabinet.Fields.FirstOrDefault(field =>
            string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase));

    private static IIndexValueValidator ValidatorFor(FieldType type) =>
        ValueValidators.TryGetValue(type, out var validator)
            ? validator
            : throw new NotSupportedException(
                $"No value validator is registered for index field type '{type}'.");

    private static string Describe(FieldType type) => type switch
    {
        FieldType.Text => "text",
        FieldType.Number => "number",
        FieldType.Date => "date",
        _ => type.ToString().ToLowerInvariant(),
    };
}
