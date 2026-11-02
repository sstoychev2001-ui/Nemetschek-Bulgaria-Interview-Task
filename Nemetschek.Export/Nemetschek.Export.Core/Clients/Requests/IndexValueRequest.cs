using System.Text.Json.Serialization;

namespace Nemetschek.Export.Core.Clients.Requests;

/// <summary>
/// One index value, as the Archive's HTTP API expects it: the type beside the value, because
/// "2026-03-01" is a date in one field and text in the next.
/// </summary>
/// <param name="Type">The field's type. The Export only ever writes <c>Text</c>.</param>
/// <param name="Value">The value itself.</param>
internal sealed record IndexValueRequest(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("value")] string Value);
