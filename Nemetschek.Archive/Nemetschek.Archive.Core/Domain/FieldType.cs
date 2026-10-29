using System.Text.Json.Serialization;
using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Domain;

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
