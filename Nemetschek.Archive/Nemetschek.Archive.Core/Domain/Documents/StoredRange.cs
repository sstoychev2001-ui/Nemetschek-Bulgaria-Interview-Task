namespace Nemetschek.Archive.Core.Domain.Documents;

/// <summary>
/// A window of time a query covers, as UTC instants. Half-open: the start is included and the end
/// is not.
/// </summary>
/// <param name="FromInclusive">The first instant in the window; <c>null</c> for unbounded.</param>
/// <param name="ToExclusive">The first instant after the window; <c>null</c> for unbounded.</param>
public sealed record StoredRange(DateTimeOffset? FromInclusive, DateTimeOffset? ToExclusive)
{
    /// <summary>Every document, whenever it was stored.</summary>
    public static StoredRange All { get; } = new(null, null);

    /// <summary>Whether an instant falls inside the window.</summary>
    public bool Includes(DateTimeOffset storedAt) =>
        (FromInclusive is null || storedAt >= FromInclusive) &&
        (ToExclusive is null || storedAt < ToExclusive);

    /// <summary>Whether a whole UTC day could hold an instant inside the window.</summary>
    public bool CouldIncludeDay(DateOnly utcDay)
    {
        var dayStart = new DateTimeOffset(utcDay.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        return (ToExclusive is null || dayStart < ToExclusive) &&
               (FromInclusive is null || dayStart.AddDays(1) > FromInclusive);
    }
}
