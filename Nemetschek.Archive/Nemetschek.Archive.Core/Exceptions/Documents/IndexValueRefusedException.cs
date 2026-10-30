namespace Nemetschek.Archive.Core.Exceptions.Documents;

/// <summary>
/// Thrown when a write of index values breaks an index field rule, so nothing is changed. The
/// message holds the rules' own user-facing sentences, one per field.
/// </summary>
public sealed class IndexValueRefusedException(IReadOnlyList<string> reasons)
    : InvalidOperationException(string.Join(" ", reasons))
{
    /// <summary>One sentence per field that was refused.</summary>
    public IReadOnlyList<string> Reasons { get; } = reasons;
}
