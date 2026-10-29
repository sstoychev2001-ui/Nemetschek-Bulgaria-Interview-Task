using Nemetschek.Archive.Api.Contracts.Uploads.Responses;
using Nemetschek.Archive.Core.Domain.Uploads;

namespace Nemetschek.Archive.Api.Contracts.Uploads.Mappings;

/// <summary>
/// Maps one entry of an upload onto the wire shape.
/// </summary>
public static class UploadEntryResultMappings
{
    /// <summary>Maps an upload entry onto the wire shape.</summary>
    public static UploadEntryResult ToResponse(this UploadEntry entry) => new(
        entry.Entry, entry.Outcome, entry.DocumentId, entry.Parts, entry.Reason);
}
