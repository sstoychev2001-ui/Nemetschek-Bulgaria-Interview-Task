using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Nemetschek.Archive.Api.Contracts.Documents.Requests;

/// <summary>
/// Index values to set on several documents of one file cabinet at once.
/// </summary>
public sealed class IndexValuesBatchRequest
{
    /// <summary>The documents to change, by the Archive's identifier.</summary>
    [Description("The documents to change, by the Archive's identifier.")]
    [Required(ErrorMessage = "Name at least one document to change.")]
    [MinLength(1, ErrorMessage = "Name at least one document to change.")]
    public List<Guid> DocumentIds { get; init; } = [];

    /// <summary>
    /// One entry per field to set, keyed by its technical name. A field that is not named is left
    /// as it is on every document.
    /// </summary>
    [Description("One entry per index field to set, keyed by the field's technical name.")]
    [Required(ErrorMessage = "Name at least one index field to set.")]
    [MinLength(1, ErrorMessage = "Name at least one index field to set.")]
    public Dictionary<string, IndexValueRequest> Values { get; init; } = [];
}
