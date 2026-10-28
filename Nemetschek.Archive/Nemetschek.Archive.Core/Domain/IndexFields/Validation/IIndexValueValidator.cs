namespace Nemetschek.Archive.Core.Domain.IndexFields.Validation;

/// <summary>
/// Validates a value against the field it is being written into, for one field type.
/// </summary>
// One implementation per FieldType. The checks that do not depend on the type — the field existing,
// being writable, and the value's type matching the field's — stay in IndexFieldRules, which selects
// the implementation and delegates the rest. Supporting a new field type means adding a validator
// rather than editing a switch.
public interface IIndexValueValidator
{
    /// <summary>The field type this validator handles.</summary>
    FieldType Type { get; }

    /// <summary>
    /// Checks <paramref name="value"/> against <paramref name="field"/>. Returns <c>null</c> when the
    /// value is acceptable, otherwise the reason it was refused.
    /// </summary>
    FieldViolation? Validate(IndexField field, IndexValue value);
}
