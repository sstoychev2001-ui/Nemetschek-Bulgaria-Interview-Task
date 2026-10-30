namespace Nemetschek.Export.Core.Domain.Exports;

/// <summary>
/// One part of a document as the Export service sees it.
/// </summary>
/// <param name="Id">The Archive's identifier, which the content is fetched by.</param>
/// <param name="Name">The file's name as the user had it.</param>
public sealed record ArchivePart(Guid Id, string Name);
