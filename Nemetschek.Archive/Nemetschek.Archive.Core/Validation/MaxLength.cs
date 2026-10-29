using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Validation;

/// <summary>The maxLength check, shared by every validator that has a length to measure.</summary>
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
