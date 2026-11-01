using Nemetschek.Export.Core.Domain.Exports;
using Nemetschek.Export.Core.Services.Interfaces;

namespace Nemetschek.Export.Api.Tests.Endpoints;

/// <summary>
/// A required setting left at its `appsettings.json` placeholder stops the service starting.
/// </summary>
public sealed class StartupSettingsTests
{
    [Fact]
    public void An_empty_archive_address_is_refused_at_startup_naming_the_setting()
    {
        using var application = new ExportApplication(new UnusedArchive())
        {
            ArchiveBaseAddress = "",
        };

        var refusal = Assert.Throws<InvalidOperationException>(() => application.CreateClient());

        Assert.Contains("Export:ArchiveBaseAddress", refusal.Message);
    }

    [Fact]
    public void Whitespace_is_refused_too_so_a_blanked_variable_cannot_pass_as_an_address()
    {
        using var application = new ExportApplication(new UnusedArchive())
        {
            ArchiveBaseAddress = "   ",
        };

        var refusal = Assert.Throws<InvalidOperationException>(() => application.CreateClient());

        Assert.Contains("Export:ArchiveBaseAddress", refusal.Message);
    }

    /// <summary>An Archive that is never reached, because startup fails first.</summary>
    private sealed class UnusedArchive : IArchiveClient
    {
        public IAsyncEnumerable<ArchiveDocument> ReadDocumentsAsync(
            string cabinetName,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Stream> OpenPartAsync(
            string cabinetName,
            Guid documentId,
            Guid partId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetTextIndexValueAsync(
            string cabinetName,
            IReadOnlyCollection<Guid> documentIds,
            string fieldName,
            string value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
