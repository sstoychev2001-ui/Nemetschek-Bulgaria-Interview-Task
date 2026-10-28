namespace Nemetschek.Archive.Core.Cabinets;

/// <summary>
/// A file cabinet: a fixed set of index fields shared by every document it holds.
/// </summary>
public sealed class Cabinet
{
    private readonly Dictionary<string, IndexField> _byName;

    public Cabinet(string name, string displayName, IReadOnlyList<IndexField> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(fields);

        Name = name;
        DisplayName = displayName;
        Fields = fields;

        // Technical names are matched case-insensitively. The fixtures contain no two fields that
        // differ only by case, so this cannot be ambiguous, and it spares callers from guessing.
        _byName = fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The technical name, used on the wire.</summary>
    public string Name { get; }

    /// <summary>The display name, shown by the client.</summary>
    public string DisplayName { get; }

    /// <summary>The index fields, in the order they are declared in cabinets.json.</summary>
    public IReadOnlyList<IndexField> Fields { get; }

    /// <summary>
    /// Finds an index field by technical name, case-insensitively. Returns <c>null</c> if this
    /// cabinet has no such field.
    /// </summary>
    public IndexField? FindField(string fieldName) =>
        fieldName is not null && _byName.TryGetValue(fieldName, out var field) ? field : null;

    /// <summary>
    /// The fields a client may choose as the upload field or the export marker field.
    /// </summary>
    public IEnumerable<IndexField> WritableTextFields => Fields.Where(f => f.IsWritableText);
}
