namespace Nemetschek.Archive.Core.Domain.Documents;

/// <summary>
/// One file belonging to a document.
/// </summary>
/// <param name="Id">The Archive's identifier for the part; also how its content is addressed.</param>
/// <param name="Name">The file's name as the user had it.</param>
/// <param name="Order">The part's position in the document, starting at zero.</param>
/// <param name="SizeInBytes">The length of the stored content.</param>
public sealed record Part(Guid Id, string Name, int Order, long SizeInBytes);
