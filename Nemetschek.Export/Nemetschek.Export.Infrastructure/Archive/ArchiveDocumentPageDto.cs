using System.Text.Json.Serialization;

namespace Nemetschek.Export.Infrastructure.Archive;

/// <summary>
/// One page of the Archive's documents, as its HTTP API describes it.
/// </summary>
internal sealed class ArchiveDocumentPageDto
{
    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("documents")]
    public List<ArchiveDocumentDto> Documents { get; init; } = [];
}
