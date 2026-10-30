using System.Text.Json.Serialization;
using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Core.Domain.IndexFields;

/// <summary>
/// A value held in an index field. One case per <see cref="FieldType"/>, so that a value carries
/// its own type and the rules never have to re-parse a string. The discriminator is written to
/// document.json as <c>type</c>, because a bare "2026-03-01" cannot say whether it is a date or
/// text.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Text), "text")]
[JsonDerivedType(typeof(Number), "number")]
[JsonDerivedType(typeof(Date), "date")]
public abstract record IndexValue
{
    /// <summary>The type this value can be stored in.</summary>
    [JsonIgnore]
    public abstract FieldType Type { get; }

    /// <summary>A text value.</summary>
    public sealed record Text([property: JsonPropertyName("value")] string Value) : IndexValue
    {
        [JsonIgnore]
        public override FieldType Type => FieldType.Text;
    }

    /// <summary>A numeric value.</summary>
    public sealed record Number([property: JsonPropertyName("value")] decimal Value) : IndexValue
    {
        [JsonIgnore]
        public override FieldType Type => FieldType.Number;
    }

    /// <summary>A calendar date.</summary>
    public sealed record Date([property: JsonPropertyName("value")] DateOnly Value) : IndexValue
    {
        [JsonIgnore]
        public override FieldType Type => FieldType.Date;
    }
}
