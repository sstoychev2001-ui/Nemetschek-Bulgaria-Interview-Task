using Nemetschek.Archive.Api.Contracts.Cabinets.Responses;
using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Validation.IndexFields;

namespace Nemetschek.Archive.Api.Contracts.Cabinets.Mappings;

/// <summary>
/// Maps the file cabinets and their index fields onto the wire shape.
/// </summary>
public static class CabinetResponseMappings
{
    /// <summary>Maps every file cabinet onto the wire shape.</summary>
    public static CabinetsResponse ToResponse(this IReadOnlyList<Cabinet> cabinets) =>
        new([.. cabinets.Select(cabinet => cabinet.ToResponse())]);

    /// <summary>Maps one file cabinet onto the wire shape.</summary>
    public static CabinetResponse ToResponse(this Cabinet cabinet) =>
        new(cabinet.Name,
            cabinet.DisplayName,
            [.. cabinet.Fields.Select(field => field.ToResponse(cabinet))]);

    private static IndexFieldResponse ToResponse(this IndexField field, Cabinet cabinet) =>
        new(field.Name,
            field.Label,
            field.Type,
            field.MaxLength,
            field.Writable,
            IndexFieldRules.ValidateChosenTextField(cabinet, field.Name) is null);
}
