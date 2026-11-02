using System.Text.Json.Serialization;
using Nemetschek.Export.Core.Domain.Exports;

namespace Nemetschek.Export.Core.Clients.Responses;

/// <summary>
/// One page of the Archive's documents, as its HTTP API describes it. The envelope is transport and
/// nothing else — the documents inside it are the ones the export rules work on.
/// </summary>
internal sealed class ArchiveDocumentPageResponse
{
    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("documents")]
    public List<ArchiveDocument> Documents { get; init; } = [];
}
