namespace Nemetschek.Archive.Core.Domain.IndexFields.Validation;

/// <summary>Validates a value written into a text field.</summary>
internal sealed class TextValueValidator : IIndexValueValidator
{
    public FieldType Type => FieldType.Text;

    public FieldViolation? Validate(IndexField field, IndexValue value) =>
        value is IndexValue.Text text ? MaxLength.Exceeded(field, text.Value.Length) : null;
}
