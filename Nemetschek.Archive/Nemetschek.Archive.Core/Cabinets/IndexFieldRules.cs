namespace Nemetschek.Archive.Core.Cabinets;

/// <summary>
/// The field rules of the Archive, in one place. Every write to an index value goes through
/// <see cref="ValidateWrite"/>; every choice of an upload field or export marker field goes through
/// <see cref="ValidateChosenTextField"/>.
///
/// These rules belong to the server. The web client may use the field metadata to shape its form,
/// but it must not re-implement the checks below.
/// </summary>
public static class IndexFieldRules
{
    /// <summary>
    /// Checks a write of <paramref name="value"/> into the field named <paramref name="fieldName"/>
    /// of <paramref name="cabinet"/>. Returns <c>null</c> when the write is allowed, otherwise the
    /// single reason it was refused.
    /// </summary>
    public static FieldViolation? ValidateWrite(Cabinet cabinet, string fieldName, IndexValue value)
    {
        ArgumentNullException.ThrowIfNull(cabinet);
        ArgumentNullException.ThrowIfNull(value);

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

        // MaxLength applies to text fields only. Length is counted in UTF-16 code units, which is
        // what a .NET string and an nvarchar(n) column both mean by "length". A string of combining
        // characters or astral-plane characters therefore costs more than it looks; that is
        // deliberate, so the limit matches what storage can actually hold.
        if (field is { Type: FieldType.Text, MaxLength: { } maxLength }
            && value is IndexValue.Text text
            && text.Value.Length > maxLength)
        {
            return new FieldViolation(
                field.Name,
                $"Index field '{field.Name}' accepts at most {maxLength} characters, " +
                $"but the value has {text.Value.Length}.");
        }

        return null;
    }

    /// <summary>
    /// Checks that <paramref name="fieldName"/> names a field of <paramref name="cabinet"/> that a
    /// client may choose as the upload field or the export marker field. Both rules require a
    /// writable text field. Returns <c>null</c> when the choice is allowed.
    /// </summary>
    public static FieldViolation? ValidateChosenTextField(Cabinet cabinet, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(cabinet);

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

    private static string Describe(FieldType type) => type switch
    {
        FieldType.Text => "text",
        FieldType.Number => "number",
        FieldType.Date => "date",
        _ => type.ToString().ToLowerInvariant(),
    };
}
