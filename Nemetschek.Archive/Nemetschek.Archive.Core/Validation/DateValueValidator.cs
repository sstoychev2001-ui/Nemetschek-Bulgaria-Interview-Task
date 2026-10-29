using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Validation;

/// <summary>Validates a value written into a date field.</summary>
internal sealed class DateValueValidator : IIndexValueValidator
{
    public FieldType Type => FieldType.Date;

    public FieldViolation? Validate(IndexField field, IndexValue value) =>
        value is IndexValue.Date date
            ? MaxLength.Exceeded(field, date.Value.ToString("yyyy-MM-dd").Length)
            : null;
}
