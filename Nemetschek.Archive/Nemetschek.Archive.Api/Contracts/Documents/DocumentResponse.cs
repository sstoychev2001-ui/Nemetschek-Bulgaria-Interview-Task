using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Api.Contracts.Documents;

/// <summary>
/// One stored document, as the client sees it.
/// </summary>
/// <param name="Id">The Archive's identifier for it.</param>
/// <param name="Title">The name of the uploaded file or folder it came from.</param>
/// <param name="IndexValues">Its index values, keyed by the field's technical name.</param>
/// <param name="Parts">Its parts, in the order they were stored.</param>
/// <param name="StoredAt">When the Archive first stored it, in UTC.</param>
/// <param name="ModifiedAt">When the Archive last changed it, in UTC.</param>
public sealed record DocumentResponse(
    Guid Id,
    string Title,
    IReadOnlyDictionary<string, IndexValueResponse> IndexValues,
    IReadOnlyList<PartResponse> Parts,
    DateTimeOffset StoredAt,
    DateTimeOffset ModifiedAt)
{
    /// <summary>Maps a stored document onto the wire shape.</summary>
    public static DocumentResponse From(Document document) => new(
        document.Id,
        document.Title,
        document.IndexValues.ToDictionary(
            entry => entry.Key,
            entry => IndexValueResponse.From(entry.Value),
            StringComparer.OrdinalIgnoreCase),
        [.. document.Parts.Select(PartResponse.From)],
        document.StoredAt,
        document.ModifiedAt);
}
