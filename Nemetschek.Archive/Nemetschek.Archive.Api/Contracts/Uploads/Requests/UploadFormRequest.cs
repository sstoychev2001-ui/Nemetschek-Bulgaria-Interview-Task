using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Nemetschek.Archive.Api.Contracts.Uploads.Requests;

/// <summary>
/// The multipart body of an upload.
/// </summary>
public sealed class UploadFormRequest
{
    /// <summary>The index field that receives the uploaded folder's name.</summary>
    [Description("The technical name of the index field the folder's name is written into.")]
    [Required(ErrorMessage = "Choose the index field the folder's name is written into.")]
    public string Field { get; init; } = "";

    /// <summary>The name of the uploaded folder.</summary>
    [Description("The uploaded folder's own name, which is written into the chosen" +
        "field on every document this upload creates.")]
    [Required(ErrorMessage = "The name of the uploaded folder is required.")]
    public string FolderName { get; init; } = "";

    /// <summary>
    /// The uploaded files. Each part's <c>filename</c> is its path relative to the uploaded
    /// folder — <c>invoice-001.txt</c>, or <c>delivery-2026-03/page1.txt</c>.
    /// </summary>
    [Description("One part per file. Each part's filename is the file's" +
        "path relative to the inside of the uploaded folder")]
    public List<IFormFile> Files { get; init; } = [];

    /// <summary>
    /// Relative paths of folders the client saw, for the two kinds its files do not imply: a folder
    /// with no file in it, and a nested folder that should be reported as skipped under its own
    /// name. A folder holding files is implied by them, and listing it changes nothing.
    /// </summary>
    [Description("Relative paths of folders the files do not already imply: one holding no file, " +
        "or a nested one to be reported as skipped under its own name. A folder containing files " +
        "is implied by them, and naming it anyway changes nothing.")]
    public List<string> Folders { get; init; } = [];
}
