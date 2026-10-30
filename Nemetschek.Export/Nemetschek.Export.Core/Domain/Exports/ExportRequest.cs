namespace Nemetschek.Export.Core.Domain.Exports;

/// <summary>
/// One export: a file cabinet, the field the marker is written into, and the dates to cover.
/// </summary>
/// <param name="CabinetName">The technical name of the file cabinet to export from.</param>
/// <param name="MarkerFieldName">The writable text field the export marker is written into.</param>
/// <param name="From">The first calendar date to include, as the user means it.</param>
/// <param name="To">The last calendar date to include, in full, as the user means it.</param>
public sealed record ExportRequest(
    string CabinetName,
    string MarkerFieldName,
    DateOnly From,
    DateOnly To);
