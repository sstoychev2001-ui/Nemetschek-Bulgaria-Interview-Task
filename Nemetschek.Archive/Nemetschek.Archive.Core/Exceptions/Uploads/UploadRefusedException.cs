namespace Nemetschek.Archive.Core.Exceptions.Uploads;

/// <summary>
/// Thrown when an upload breaks an index field rule, so nothing is stored. The message is the
/// rule's own user-facing sentence, naming the field and the limit.
/// </summary>
public sealed class UploadRefusedException(string fieldName, string reason)
    : InvalidOperationException(reason)
{
    /// <summary>The technical name of the field the upload was refused over.</summary>
    public string FieldName { get; } = fieldName;
}
