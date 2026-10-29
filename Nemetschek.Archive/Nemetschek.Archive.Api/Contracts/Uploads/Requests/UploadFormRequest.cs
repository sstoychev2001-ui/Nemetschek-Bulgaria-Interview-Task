using System.ComponentModel.DataAnnotations;

namespace Nemetschek.Archive.Api.Contracts.Uploads.Requests;

/// <summary>
/// The multipart body of an upload.
/// </summary>
public sealed class UploadFormRequest
{
    /// <summary>The index field that receives the uploaded folder's name.</summary>
    [Required(ErrorMessage = "Choose the index field the folder's name is written into.")]
    public string Field { get; init; } = "";

    /// <summary>The name of the uploaded folder.</summary>
    [Required(ErrorMessage = "The name of the uploaded folder is required.")]
    public string FolderName { get; init; } = "";

    /// <summary>
    /// The uploaded files. Each part's <c>filename</c> is its path relative to the uploaded
    /// folder — <c>invoice-001.txt</c>, or <c>delivery-2026-03/page1.txt</c>.
    /// </summary>
    public List<IFormFile> Files { get; init; } = [];

    /// <summary>
    /// Relative paths of folders the client saw. Only folders holding no file need to be listed;
    /// a folder containing files is implied by them.
    /// </summary>
    public List<string> Folders { get; init; } = [];
}
