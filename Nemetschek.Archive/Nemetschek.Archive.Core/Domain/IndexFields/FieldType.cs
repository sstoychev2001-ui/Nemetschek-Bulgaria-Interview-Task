using System.Text.Json.Serialization;
using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Core.Domain.IndexFields;

/// <summary>
/// The type of an index field, as declared in cabinets.json.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FieldType>))]
public enum FieldType
{
    Text,
    Number,
    Date
}
