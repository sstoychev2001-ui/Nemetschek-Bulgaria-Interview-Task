namespace Nemetschek.Archive.Core.Domain.Documents;

/// <summary>
/// A part as it is handed to the store: a name and a way to read its content once.
/// </summary>
/// <param name="Name">The file's name as the user had it.</param>
/// <param name="OpenContent">Opens the content for reading. The store disposes the stream.</param>
public sealed record PartDraft(string Name, Func<Stream> OpenContent);
