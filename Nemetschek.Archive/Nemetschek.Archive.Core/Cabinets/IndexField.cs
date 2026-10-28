namespace Nemetschek.Archive.Core.Cabinets;

/// <summary>
/// One index field of a file cabinet, as declared in cabinets.json. Cabinets and their fields are
/// fixed: there is no way to create, change or delete them.
/// </summary>
/// <param name="Name">The technical name, used on the wire.</param>
/// <param name="Label">The display label, shown by the client.</param>
/// <param name="Type">The field's type.</param>
/// <param name="MaxLength">
/// The maximum length, for <see cref="FieldType.Text"/> fields only; <c>null</c> otherwise.
/// </param>
/// <param name="Writable">Whether a client may set this field.</param>
public sealed record IndexField(
    string Name,
    string Label,
    FieldType Type,
    int? MaxLength,
    bool Writable)
{
    /// <summary>
    /// Whether this field can be chosen as the upload field or the export marker field. Both the
    /// upload rules and the export rules require a writable text field.
    /// </summary>
    public bool IsWritableText => Writable && Type == FieldType.Text;
}
