using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Validation.IndexFields;

/// <summary>Validates a value written into a date field.</summary>
internal sealed class DateValueValidator : IIndexValueValidator
{
    public FieldType Type => FieldType.Date;

    public FieldViolation? Validate(IndexField field, IndexValue value) =>
        value is IndexValue.Date date
            ? MaxLength.Exceeded(field, date.Value.ToString("yyyy-MM-dd").Length)
            : null;
}
