namespace Nemetschek.Archive.Core.Domain.Uploads;

/// <summary>
/// One file as it arrived: where it sat in the uploaded folder, and how to read it.
/// </summary>
/// <param name="RelativePath">The path relative to the uploaded folder. Untrusted.</param>
/// <param name="OpenContent">Opens the content for reading. The store disposes the stream.</param>
public sealed record UploadedFile(string RelativePath, Func<Stream> OpenContent);
