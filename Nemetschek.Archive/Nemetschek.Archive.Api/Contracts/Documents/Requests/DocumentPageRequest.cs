using System.ComponentModel.DataAnnotations;

namespace Nemetschek.Archive.Api.Contracts.Documents.Requests;

/// <summary>
/// The query string of a request for a page of documents.
/// </summary>
public sealed class DocumentPageRequest
{
    /// <summary>The page to read, starting at one.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    /// <summary>How many documents the page should hold, capped by the configured maximum.</summary>
    [Range(1, int.MaxValue)]
    public int Size { get; init; } = 20;

    /// <summary>How many documents this page holds, within the server's maximum.</summary>
    public int Take(int maxPageSize) => Math.Clamp(Size, 1, maxPageSize);

    /// <summary>How many documents come before this page.</summary>
    public int Skip(int maxPageSize) => (Math.Max(Page, 1) - 1) * Take(maxPageSize);
}
