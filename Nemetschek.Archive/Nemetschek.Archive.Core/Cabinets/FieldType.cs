namespace Nemetschek.Archive.Core.Cabinets;

/// <summary>
/// The type of an index field, as declared in cabinets.json.
/// </summary>
public enum FieldType
{
    /// <summary>A string value, length-limited by <see cref="IndexField.MaxLength"/>.</summary>
    Text,

    /// <summary>A numeric value, carried as <see cref="decimal"/>.</summary>
    Number,

    /// <summary>A calendar date with no time and no offset.</summary>
    Date,
}
