namespace Nemetschek.Archive.Api;

/// <summary>
/// The largest page the Archive will return.
/// </summary>
/// <param name="MaxPageSize">Most documents a single page may hold.</param>
public sealed record PagingLimits(int MaxPageSize);
