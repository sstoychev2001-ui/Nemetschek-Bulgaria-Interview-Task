using System.Globalization;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Api.Contracts.Documents;

/// <summary>
/// One index value, as the client sees it.
/// </summary>
/// <param name="Type">Whether the value is <c>Text</c>, <c>Number</c> or <c>Date</c>.</param>
/// <param name="Value">The value itself, in the shape the type implies.</param>
public sealed record IndexValueResponse(FieldType Type, object Value)
{
    /// <summary>Maps an index value onto the wire shape.</summary>
    /// <exception cref="NotSupportedException">The value has a type with no wire shape.</exception>
    public static IndexValueResponse From(IndexValue value) => value switch
    {
        IndexValue.Text text => new IndexValueResponse(value.Type, text.Value),
        IndexValue.Number number => new IndexValueResponse(value.Type, number.Value),
        IndexValue.Date date => new IndexValueResponse(
            value.Type, date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        _ => throw new NotSupportedException($"No wire shape is defined for '{value.Type}' values."),
    };
}
