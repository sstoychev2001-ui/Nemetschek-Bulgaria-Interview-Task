using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Domain.Documents;

/// <summary>
/// One record in a file cabinet: its index values plus zero or more parts.
/// </summary>
/// <param name="Id">The Archive's identifier for the document.</param>
/// <param name="CabinetName">The technical name of the file cabinet holding it.</param>
/// <param name="Title">The name of the uploaded file or folder it came from.</param>
/// <param name="IndexValues">The index values, keyed by the field's technical name.</param>
/// <param name="Parts">The parts, in the order they were stored.</param>
/// <param name="StoredAt">When the Archive first stored it, in UTC.</param>
/// <param name="ModifiedAt">When the Archive last changed it, in UTC.</param>
public sealed record Document(
    Guid Id,
    string CabinetName,
    string Title,
    IReadOnlyDictionary<string, IndexValue> IndexValues,
    IReadOnlyList<Part> Parts,
    DateTimeOffset StoredAt,
    DateTimeOffset ModifiedAt);
