using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Domain;

/// <summary>
/// What an upload did: one entry per thing the Archive saw.
/// </summary>
/// <param name="CabinetName">The file cabinet that was stored into.</param>
/// <param name="FieldName">The index field that received the folder's name.</param>
/// <param name="FolderName">The value written into that field on every stored document.</param>
/// <param name="Results">One result per entry, ordered by entry name.</param>
public sealed record UploadReport(
    string CabinetName,
    string FieldName,
    string FolderName,
    IReadOnlyList<UploadEntry> Results);
