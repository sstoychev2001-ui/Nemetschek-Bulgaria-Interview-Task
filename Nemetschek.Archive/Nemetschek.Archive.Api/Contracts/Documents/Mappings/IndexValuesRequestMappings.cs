using Nemetschek.Archive.Api.Contracts.Documents.Requests;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Api.Contracts.Documents.Mappings;

/// <summary>
/// Maps index values on the way in onto the values the rules take.
/// </summary>
public static class IndexValuesRequestMappings
{
    /// <summary>
    /// Maps the body onto the values to set, keyed case-insensitively as a cabinet names its fields.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A value does not parse as its own type. Model validation refuses that first, so reaching this
    /// means validation was bypassed.
    /// </exception>
    public static IReadOnlyDictionary<string, IndexValue> ToIndexValues(
        this IndexValuesBatchRequest request) =>
        request.Values.ToDictionary(
            value => value.Key,
            value => value.Value.ToIndexValue()
                     ?? throw new InvalidOperationException(
                         $"'{value.Value.Value}' is not a usable {value.Value.Type} value."),
            StringComparer.OrdinalIgnoreCase);
}
