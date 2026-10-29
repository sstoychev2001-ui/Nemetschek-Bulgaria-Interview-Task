using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Domain;

/// <summary>
/// One page of documents, with the total the page was taken from.
/// </summary>
/// <param name="Documents">The documents on this page, oldest first.</param>
/// <param name="Total">How many documents the cabinet holds in all.</param>
public sealed record DocumentPage(IReadOnlyList<Document> Documents, int Total);
