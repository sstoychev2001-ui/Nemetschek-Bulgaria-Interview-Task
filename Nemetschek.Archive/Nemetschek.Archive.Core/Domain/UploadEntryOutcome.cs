using System.Text.Json.Serialization;
using Nemetschek.Archive.Core.Domain;
using Nemetschek.Archive.Core.Services;
using Nemetschek.Archive.Core.Services.Interfaces;
using Nemetschek.Archive.Core.Validation;
using Nemetschek.Archive.Core.Exceptions;
namespace Nemetschek.Archive.Core.Domain;

/// <summary>
/// The three things that can become of an uploaded entry, from plan to report.
/// </summary>
// One vocabulary for both stages. Planning says what it intends — `Stored` or `Skipped` — and
// storing returns the verdict, which can turn an intended `Stored` into `Failed`. Skipped and
// Failed are not the same answer and must not be merged: skipped is the rules working as written
// and needs nothing from the user, failed is the Archive falling short and can be retried.
// `Failed` is also what UploadFailureStatusMiddleware watches for.
// Skipped and Failed are not the same answer and must not be merged: skipped is the rules working
// as written and needs nothing from the user, failed is the Archive falling short and can be
// retried. Failed is also what UploadFailureStatusMiddleware watches for.
[JsonConverter(typeof(JsonStringEnumConverter<UploadEntryOutcome>))]
public enum UploadEntryOutcome
{
    Stored,
    Skipped,
    Failed
}
