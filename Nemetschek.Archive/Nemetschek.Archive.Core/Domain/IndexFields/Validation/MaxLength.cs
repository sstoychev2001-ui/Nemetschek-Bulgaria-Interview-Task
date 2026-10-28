namespace Nemetschek.Archive.Core.Domain.IndexFields.Validation;

/// <summary>The maxLength check, shared by every validator that has a length to measure.</summary>
// Length is counted in UTF-16 code units, which is what a .NET string and an nvarchar(n) column both
// mean by "length". A string of combining characters or astral-plane characters therefore costs more
// than it looks; that is deliberate, so the limit matches what storage can actually hold.
internal static class MaxLength
{
    public static FieldViolation? Exceeded(IndexField field, int length) =>
        field.MaxLength is { } maxLength && length > maxLength
            ? new FieldViolation(
                field.Name,
                $"Index field '{field.Name}' accepts at most {maxLength} characters, " +
                $"but the value has {length}.")
            : null;
}
