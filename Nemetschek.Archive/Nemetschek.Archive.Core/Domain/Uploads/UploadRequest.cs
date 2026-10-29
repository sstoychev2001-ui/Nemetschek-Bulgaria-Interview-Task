namespace Nemetschek.Archive.Core.Domain.Uploads;

/// <summary>
/// One upload: a file cabinet, the index field to stamp, and the contents of a folder.
/// </summary>
/// <param name="CabinetName">The technical name of the file cabinet to store into.</param>
/// <param name="FieldName">The index field that receives the folder's name.</param>
/// <param name="FolderName">The name of the uploaded folder.</param>
/// <param name="Files">Every uploaded file, with its path relative to the uploaded folder.</param>
/// <param name="FolderPaths">
/// Relative paths of folders the client saw; only folders holding no file need to be listed.
/// </param>
public sealed record UploadRequest(
    string CabinetName,
    string FieldName,
    string FolderName,
    IReadOnlyList<UploadedFile> Files,
    IReadOnlyList<string> FolderPaths);
