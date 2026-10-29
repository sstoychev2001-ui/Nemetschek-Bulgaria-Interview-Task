using System.ComponentModel.DataAnnotations;

namespace Nemetschek.Archive.Api.Contracts.Documents;

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
    // Clamped rather than rejected: model validation is suppressed so that no endpoint produces a
    // status of its own, and a caller asking for a million documents should get the maximum rather
    // than an error. The DataAnnotations above stay for OpenAPI.
    public int Take(int maxPageSize) => Math.Clamp(Size, 1, maxPageSize);

    /// <summary>How many documents come before this page.</summary>
    public int Skip(int maxPageSize) => (Math.Max(Page, 1) - 1) * Take(maxPageSize);
}
