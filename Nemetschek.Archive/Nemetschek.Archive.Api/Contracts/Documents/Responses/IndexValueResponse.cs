using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Api.Contracts.Documents.Responses;

/// <summary>
/// One index value, as the client sees it. It carries its type so a client never has to infer one
/// from the JSON: "2026-03-01" is a date here and could be plain text in the next field along.
/// </summary>
/// <param name="Type">Whether the value is <c>Text</c>, <c>Number</c> or <c>Date</c>.</param>
/// <param name="Value">The value itself, in the shape the type implies.</param>
public sealed record IndexValueResponse(FieldType Type, object Value);
