using Nemetschek.Archive.Api.Contracts.Uploads.Requests;
using Nemetschek.Archive.Core.Domain.Uploads;

namespace Nemetschek.Archive.Api.Contracts.Uploads.Mappings;

/// <summary>
/// Maps an upload's multipart body onto the upload the Archive's rules take.
/// </summary>
public static class UploadFormRequestMappings
{
    /// <summary>
    /// Maps the body onto an upload request. Each file's <c>filename</c> is the path it had inside
    /// the uploaded folder, and is untrusted.
    /// </summary>
    public static UploadRequest ToUploadRequest(this UploadFormRequest form, string cabinetName) =>
        new(
            cabinetName,
            form.Field,
            form.FolderName,
            [.. form.Files.Select(file => new UploadedFile(file.FileName, file.OpenReadStream))],
            form.Folders);
}
