using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Exceptions.Documents;

namespace Nemetschek.Archive.Core.Validation.Documents;

/// <summary>
/// Reads a from-date and a to-date as calendar dates, both inclusive, in UTC.
/// </summary>
// The dates carry no clock time, so a zone has to be chosen to compare them against StoredAt, and
// that zone is UTC. A caller whose own day differs from the UTC day therefore sees UTC's boundaries
// rather than their own — the trade is recorded under "Rejected: a per-request time zone" in
// DECISIONS.md, together with what it costs.
public static class CalendarRange
{
    /// <summary>
    /// Turns two optional calendar dates into a half-open window of UTC instants. The to-date's
    /// whole day is covered, which is what "inclusive" means to a reader.
    /// </summary>
    /// <exception cref="InvalidDateRangeException">The window ends before it starts.</exception>
    public static StoredRange Of(DateOnly? from, DateOnly? to)
    {
        if (from is { } start && to is { } end && end < start)
        {
            throw new InvalidDateRangeException(start, end);
        }

        return new StoredRange(
            from is { } fromDate ? StartOfDay(fromDate) : null,
            to is { } toDate ? StartOfDay(toDate.AddDays(1)) : null);
    }

    private static DateTimeOffset StartOfDay(DateOnly day) =>
        new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
