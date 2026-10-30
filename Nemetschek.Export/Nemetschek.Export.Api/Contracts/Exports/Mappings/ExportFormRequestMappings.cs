using Nemetschek.Export.Api.Contracts.Exports.Requests;
using Nemetschek.Export.Core.Domain.Exports;

namespace Nemetschek.Export.Api.Contracts.Exports.Mappings;

/// <summary>
/// Maps an export's body onto the export the rules take.
/// </summary>
public static class ExportFormRequestMappings
{
    /// <summary>
    /// Maps the body onto an export request. Model validation has already refused a body with no
    /// dates, which is why reading them here is not conditional.
    /// </summary>
    public static ExportRequest ToExportRequest(this ExportFormRequest form) =>
        new(form.Cabinet, form.MarkerField, form.From!.Value, form.To!.Value);

    /// <summary>The file name the archive is offered to the caller under.</summary>
    public static string ToFileName(this ExportFormRequest form) =>
        $"{form.Cabinet}-{form.From:yyyy-MM-dd}-to-{form.To:yyyy-MM-dd}.zip";
}
