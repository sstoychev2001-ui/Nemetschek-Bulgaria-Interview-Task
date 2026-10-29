using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Domain;

/// <summary>
/// One file as it arrived: where it sat in the uploaded folder, and how to read it.
/// </summary>
/// <param name="RelativePath">The path relative to the uploaded folder. Untrusted.</param>
/// <param name="OpenContent">Opens the content for reading. The store disposes the stream.</param>
public sealed record UploadedFile(string RelativePath, Func<Stream> OpenContent);
