using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Exceptions.Documents;
using Nemetschek.Archive.Core.Validation.Documents;

namespace Nemetschek.Archive.Core.Tests.Validation.Documents;

/// <summary>
/// Reading a from-date and a to-date as calendar dates, both inclusive, in UTC.
/// </summary>
[TestClass]
public sealed class CalendarRangeTests
{
    [TestMethod]
    public void A_window_starts_at_midnight_UTC_on_the_from_date()
    {
        var range = CalendarRange.Of(new DateOnly(2026, 3, 1), null);

        Assert.AreEqual(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), range.FromInclusive);
        Assert.IsNull(range.ToExclusive);
    }

    [TestMethod]
    public void The_to_date_is_included_in_full()
    {
        var range = CalendarRange.Of(null, new DateOnly(2026, 3, 1));

        Assert.IsTrue(range.Includes(new DateTimeOffset(2026, 3, 1, 23, 59, 59, TimeSpan.Zero)));
        Assert.IsFalse(range.Includes(new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero)));
    }

    [TestMethod]
    public void A_single_day_window_covers_that_day_and_nothing_else()
    {
        var day = new DateOnly(2026, 3, 1);
        var range = CalendarRange.Of(day, day);

        Assert.IsFalse(range.Includes(new DateTimeOffset(2026, 2, 28, 23, 59, 59, TimeSpan.Zero)));
        Assert.IsTrue(range.Includes(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.IsTrue(range.Includes(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero)));
        Assert.IsFalse(range.Includes(new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero)));
    }

    [TestMethod]
    public void An_instant_is_judged_by_its_UTC_day_whatever_offset_it_carries()
    {
        var day = new DateOnly(2026, 3, 1);
        var range = CalendarRange.Of(day, day);

        Assert.IsFalse(range.Includes(new DateTimeOffset(2026, 3, 1, 0, 30, 0, TimeSpan.FromHours(2))));
        Assert.IsTrue(range.Includes(new DateTimeOffset(2026, 3, 1, 2, 30, 0, TimeSpan.FromHours(2))));
    }

    [TestMethod]
    public void A_window_of_several_days_runs_from_the_first_midnight_to_the_last_midnight_but_one()
    {
        var range = CalendarRange.Of(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        Assert.AreEqual(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), range.FromInclusive);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), range.ToExclusive);
    }

    [TestMethod]
    public void A_window_over_a_leap_day_covers_it()
    {
        var range = CalendarRange.Of(new DateOnly(2028, 2, 28), new DateOnly(2028, 2, 29));

        Assert.IsTrue(range.Includes(new DateTimeOffset(2028, 2, 29, 12, 0, 0, TimeSpan.Zero)));
        Assert.IsFalse(range.Includes(new DateTimeOffset(2028, 3, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [TestMethod]
    public void No_dates_at_all_is_every_document()
    {
        var range = CalendarRange.Of(null, null);

        Assert.IsNull(range.FromInclusive);
        Assert.IsNull(range.ToExclusive);
        Assert.IsTrue(range.Includes(DateTimeOffset.UnixEpoch));
        Assert.IsTrue(range.Includes(DateTimeOffset.MaxValue));
    }

    [TestMethod]
    public void A_to_date_before_the_from_date_is_refused()
    {
        var failure = Assert.ThrowsExactly<InvalidDateRangeException>(() =>
            CalendarRange.Of(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 1)));

        Assert.Contains("2026-03-01", failure.Message, StringComparison.Ordinal);
        Assert.Contains("2026-03-02", failure.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void The_same_date_twice_is_allowed()
    {
        var day = new DateOnly(2026, 3, 1);

        Assert.IsNotNull(CalendarRange.Of(day, day).FromInclusive);
    }

    [TestMethod]
    public void Every_day_is_possible_when_there_is_no_window()
    {
        Assert.IsTrue(StoredRange.All.CouldIncludeDay(new DateOnly(1970, 1, 1)));
        Assert.IsTrue(StoredRange.All.CouldIncludeDay(new DateOnly(2999, 12, 31)));
    }
}
