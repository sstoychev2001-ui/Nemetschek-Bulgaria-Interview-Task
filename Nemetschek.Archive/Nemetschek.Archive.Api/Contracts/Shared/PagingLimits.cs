namespace Nemetschek.Archive.Api.Contracts.Shared;

/// <summary>
/// The largest page the Archive will return. Configurable because the brief requires it, and
/// because the review demo runs with it set to 5.
/// </summary>
/// <param name="MaxPageSize">Most documents a single page may hold.</param>
public sealed record PagingLimits(int MaxPageSize);
