using System.Text.Json.Serialization;

namespace Nemetschek.Export.Core.Clients.Requests;

/// <summary>
/// The body of the Archive's batch index-value write, as its HTTP API expects it.
/// </summary>
/// <param name="DocumentIds">The documents to write onto.</param>
/// <param name="Values">The values to set on each of them, keyed by the field's technical name.</param>
internal sealed record IndexValuesBatchRequest(
    [property: JsonPropertyName("documentIds")] IReadOnlyCollection<Guid> DocumentIds,
    [property: JsonPropertyName("values")] IReadOnlyDictionary<string, IndexValueRequest> Values);
