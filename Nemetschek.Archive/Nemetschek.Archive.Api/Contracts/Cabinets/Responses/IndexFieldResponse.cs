using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Api.Contracts.Cabinets.Responses;

/// <summary>
/// One index field of a file cabinet, as the client sees it.
/// </summary>
/// <param name="Name">The technical name, used on the wire and as the key of an index value.</param>
/// <param name="Label">The display label.</param>
/// <param name="Type">Whether the field holds <c>Text</c>, a <c>Number</c> or a <c>Date</c>.</param>
/// <param name="MaxLength">The longest value the field accepts; <c>null</c> for unbounded.</param>
/// <param name="Writable">Whether a client may set this field at all.</param>
/// <param name="Choosable">
/// Whether a client may offer this field as the upload field or the export marker field. The
/// Archive answers it so the client never restates the rule that both must be writable text.
/// </param>
public sealed record IndexFieldResponse(
    string Name,
    string Label,
    FieldType Type,
    int? MaxLength,
    bool Writable,
    bool Choosable);
