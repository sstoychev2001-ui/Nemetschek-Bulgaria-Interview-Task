using System.Text.Json.Serialization;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Domain.Uploads;

/// <summary>
/// The three things that can become of an uploaded entry, from plan to report.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<UploadEntryOutcome>))]
public enum UploadEntryOutcome
{
    Stored,
    Skipped,
    Failed
}
