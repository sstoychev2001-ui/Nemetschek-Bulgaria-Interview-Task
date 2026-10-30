using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Exceptions.Documents;
using Nemetschek.Archive.Core.Validation.Documents;

namespace Nemetschek.Archive.Core.Tests.Documents;

/// <summary>
/// Reading a from-date and a to-date as calendar dates, both inclusive, in UTC.
/// </summary>
public sealed class CalendarRangeTests
{
    [Fact]
    public void A_window_starts_at_midnight_UTC_on_the_from_date()
    {
        var range = CalendarRange.Of(new DateOnly(2026, 3, 1), null);

        Assert.Equal(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), range.FromInclusive);
        Assert.Null(range.ToExclusive);
    }

    [Fact]
    public void The_to_date_is_included_in_full()
    {
        var range = CalendarRange.Of(null, new DateOnly(2026, 3, 1));

        Assert.True(range.Includes(new DateTimeOffset(2026, 3, 1, 23, 59, 59, TimeSpan.Zero)));
        Assert.False(range.Includes(new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void A_single_day_window_covers_that_day_and_nothing_else()
    {
        var day = new DateOnly(2026, 3, 1);
        var range = CalendarRange.Of(day, day);

        Assert.False(range.Includes(new DateTimeOffset(2026, 2, 28, 23, 59, 59, TimeSpan.Zero)));
        Assert.True(range.Includes(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.True(range.Includes(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero)));
        Assert.False(range.Includes(new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void An_instant_is_judged_by_its_UTC_day_whatever_offset_it_carries()
    {
        // 00:30 on the 1st at +02:00 is 22:30 UTC on the 28th of February, so it belongs to the
        // 28th. That is the cost of reading dates in UTC rather than in the caller's zone, and it is
        // the whole content of "Removed: the time zone parameter" in DECISIONS.md.
        var day = new DateOnly(2026, 3, 1);
        var range = CalendarRange.Of(day, day);

        Assert.False(range.Includes(new DateTimeOffset(2026, 3, 1, 0, 30, 0, TimeSpan.FromHours(2))));
        Assert.True(range.Includes(new DateTimeOffset(2026, 3, 1, 2, 30, 0, TimeSpan.FromHours(2))));
    }

    [Fact]
    public void A_window_of_several_days_runs_from_the_first_midnight_to_the_last_midnight_but_one()
    {
        var range = CalendarRange.Of(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.Equal(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), range.FromInclusive);
        Assert.Equal(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), range.ToExclusive);
    }

    [Fact]
    public void A_window_over_a_leap_day_covers_it()
    {
        var range = CalendarRange.Of(new DateOnly(2028, 2, 28), new DateOnly(2028, 2, 29));

        Assert.True(range.Includes(new DateTimeOffset(2028, 2, 29, 12, 0, 0, TimeSpan.Zero)));
        Assert.False(range.Includes(new DateTimeOffset(2028, 3, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void No_dates_at_all_is_every_document()
    {
        var range = CalendarRange.Of(null, null);

        Assert.Null(range.FromInclusive);
        Assert.Null(range.ToExclusive);
        Assert.True(range.Includes(DateTimeOffset.UnixEpoch));
        Assert.True(range.Includes(DateTimeOffset.MaxValue));
    }

    [Fact]
    public void A_to_date_before_the_from_date_is_refused()
    {
        var failure = Assert.Throws<InvalidDateRangeException>(() =>
            CalendarRange.Of(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 1)));

        Assert.Contains("2026-03-01", failure.Message, StringComparison.Ordinal);
        Assert.Contains("2026-03-02", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_date_twice_is_allowed()
    {
        var day = new DateOnly(2026, 3, 1);

        Assert.NotNull(CalendarRange.Of(day, day).FromInclusive);
    }

    [Theory]
    [InlineData("2026-02-28", false)]
    [InlineData("2026-03-01", true)]
    [InlineData("2026-03-02", false)]
    public void A_whole_UTC_day_outside_the_window_can_be_pruned(string day, bool couldInclude)
    {
        var date = new DateOnly(2026, 3, 1);
        var range = CalendarRange.Of(date, date);

        Assert.Equal(couldInclude, range.CouldIncludeDay(DateOnly.Parse(day)));
    }

    [Fact]
    public void Every_day_is_possible_when_there_is_no_window()
    {
        Assert.True(StoredRange.All.CouldIncludeDay(new DateOnly(1970, 1, 1)));
        Assert.True(StoredRange.All.CouldIncludeDay(new DateOnly(2999, 12, 31)));
    }
}
