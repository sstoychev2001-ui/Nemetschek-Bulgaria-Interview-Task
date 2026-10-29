using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.IndexFields.Validation;

namespace Nemetschek.Archive.Core.Domain.IndexFields;

/// <summary>
/// The index field rules of the Archive, in one place.
/// </summary>
public static class IndexFieldRules
{
    // One validator per field type. Supporting a new type means adding a validator rather than
    // editing this class; a type with none registered throws, so validation cannot be skipped by
    // omission.
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
        var field = cabinet.FindField(fieldName);
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

        // Everything above holds for every field type. What remains depends on the type, so it is the
        // type's own validator that decides.
        return ValidatorFor(field.Type).Validate(field, value);
    }

    /// <summary>
    /// Checks that a field may be chosen as the upload field or the export marker field. Returns
    /// <c>null</c> when the choice is allowed, otherwise the reason it was refused.
    /// </summary>
    public static FieldViolation? ValidateChosenTextField(Cabinet cabinet, string fieldName)
    {
        var field = cabinet.FindField(fieldName);
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
