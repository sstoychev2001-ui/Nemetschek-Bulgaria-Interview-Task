using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Validation;

/// <summary>Validates a value written into a text field.</summary>
internal sealed class TextValueValidator : IIndexValueValidator
{
    public FieldType Type => FieldType.Text;

    public FieldViolation? Validate(IndexField field, IndexValue value) =>
        value is IndexValue.Text text ? MaxLength.Exceeded(field, text.Value.Length) : null;
}
