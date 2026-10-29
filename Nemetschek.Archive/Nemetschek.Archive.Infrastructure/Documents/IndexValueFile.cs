using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Infrastructure.Documents;

/// <summary>
/// One index value as it is written to document.json.
/// </summary>
internal sealed class IndexValueFile
{
    /// <summary>Which kind of value this is.</summary>
    [JsonPropertyName("type")]
    public required FieldType Type { get; init; }

    /// <summary>The value itself, in the shape the type implies.</summary>
    [JsonPropertyName("value")]
    public JsonElement Value { get; init; }

    /// <summary>Maps a stored index value onto the domain.</summary>
    /// <exception cref="NotSupportedException">The file declares a type with no mapping.</exception>
    public IndexValue ToIndexValue() => Type switch
    {
        FieldType.Text => new IndexValue.Text(Value.GetString() ?? ""),
        FieldType.Number => new IndexValue.Number(Value.GetDecimal()),
        FieldType.Date => new IndexValue.Date(DateOnly.ParseExact(
            Value.GetString() ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture)),
        _ => throw new NotSupportedException($"No stored shape is defined for '{Type}' values."),
    };

    /// <summary>Maps a domain index value onto the stored shape.</summary>
    /// <exception cref="NotSupportedException">The value has a type with no mapping.</exception>
    public static IndexValueFile From(IndexValue value) => new()
    {
        Type = value.Type,
        Value = value switch
        {
            IndexValue.Text text => JsonSerializer.SerializeToElement(text.Value),
            IndexValue.Number number => JsonSerializer.SerializeToElement(number.Value),
            IndexValue.Date date => JsonSerializer.SerializeToElement(
                date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            _ => throw new NotSupportedException(
                $"No stored shape is defined for '{value.Type}' values."),
        },
    };
}
