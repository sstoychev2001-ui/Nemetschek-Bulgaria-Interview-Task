namespace Nemetschek.Export.Core.Domain.Exports;

/// <summary>
/// One document as the Export service sees it, read from the Archive over HTTP.
/// </summary>
/// <param name="Id">The Archive's identifier.</param>
/// <param name="Title">The document's title, which the zip entry is named after.</param>
/// <param name="Parts">Its parts, in the order the Archive stored them.</param>
public sealed record ArchiveDocument(Guid Id, string Title, IReadOnlyList<ArchivePart> Parts);
