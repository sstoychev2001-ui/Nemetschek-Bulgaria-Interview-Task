namespace Nemetschek.Archive.Core.Exceptions.Documents;

/// <summary>
/// Thrown when a query's from-date and to-date cannot describe a window of time.
/// </summary>
public sealed class InvalidDateRangeException(DateOnly from, DateOnly to)
    : InvalidOperationException(
        $"The to-date {to:yyyy-MM-dd} is before the from-date {from:yyyy-MM-dd}.");
