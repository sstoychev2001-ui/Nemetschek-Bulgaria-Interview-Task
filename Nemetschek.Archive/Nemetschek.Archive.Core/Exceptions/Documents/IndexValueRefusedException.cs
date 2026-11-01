using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Exceptions.Documents;

/// <summary>
/// Thrown when a write of index values breaks an index field rule, so nothing is changed. The
/// message holds the rules' own user-facing sentences, one per field that was refused.
/// </summary>
public sealed class IndexValueRefusedException(IReadOnlyList<FieldViolation> violations)
    : InvalidOperationException(
        string.Join(" ", violations.Select(violation => violation.Reason)))
{
    /// <summary>One violation per field that was refused.</summary>
    public IReadOnlyList<FieldViolation> Violations { get; } = violations;
}
