using System.Globalization;
using Nemetschek.Archive.Api.Contracts.Documents.Responses;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Api.Contracts.Documents.Mappings;

/// <summary>
/// Maps an index value onto the wire shape.
/// </summary>
public static class IndexValueResponseMappings
{
    /// <summary>Maps an index value onto the wire shape.</summary>
    /// <exception cref="NotSupportedException">The value has a type with no wire shape.</exception>
    public static IndexValueResponse ToResponse(this IndexValue value) => value switch
    {
        IndexValue.Text text => new IndexValueResponse(value.Type, text.Value),
        IndexValue.Number number => new IndexValueResponse(value.Type, number.Value),
        IndexValue.Date date => new IndexValueResponse(
            value.Type, date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        _ => throw new NotSupportedException($"No wire shape is defined for '{value.Type}' values."),
    };
}
