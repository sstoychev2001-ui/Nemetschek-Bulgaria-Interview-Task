using System.Globalization;
using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Validation;

/// <summary>Validates a value written into a number field.</summary>
internal sealed class NumberValueValidator : IIndexValueValidator
{
    public FieldType Type => FieldType.Number;

    public FieldViolation? Validate(IndexField field, IndexValue value) =>
        value is IndexValue.Number number
            ? MaxLength.Exceeded(field, number.Value.ToString(CultureInfo.InvariantCulture).Length)
            : null;
}
