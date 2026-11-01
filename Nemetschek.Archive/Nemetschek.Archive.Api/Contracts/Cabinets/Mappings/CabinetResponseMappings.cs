using Nemetschek.Archive.Api.Contracts.Cabinets.Responses;
using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.IndexFields;

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
    public static CabinetResponse ToResponse(this Cabinet cabinet)
    {
        var choosable = cabinet.WritableTextFields.Select(field => field.Name).ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        return new CabinetResponse(
            cabinet.Name,
            cabinet.DisplayName,
            [.. cabinet.Fields.Select(field => field.ToResponse(choosable.Contains(field.Name)))]);
    }

    private static IndexFieldResponse ToResponse(this IndexField field, bool choosable) =>
        new(field.Name, field.Label, field.Type, field.MaxLength, field.Writable, choosable);
}
