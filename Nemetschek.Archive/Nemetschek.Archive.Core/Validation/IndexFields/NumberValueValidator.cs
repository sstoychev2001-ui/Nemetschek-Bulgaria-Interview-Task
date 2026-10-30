using System.Globalization;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Validation.IndexFields;

/// <summary>Validates a value written into a number field.</summary>
internal sealed class NumberValueValidator : IIndexValueValidator
{
    public FieldType Type => FieldType.Number;

    public FieldViolation? Validate(IndexField field, IndexValue value) =>
        value is IndexValue.Number number
            ? MaxLength.Exceeded(field, number.Value.ToString(CultureInfo.InvariantCulture).Length)
            : null;
}
