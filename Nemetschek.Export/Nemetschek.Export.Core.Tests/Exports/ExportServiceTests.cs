using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Nemetschek.Export.Core.Domain.Exports;
using Nemetschek.Export.Core.Services.Exports;

namespace Nemetschek.Export.Core.Tests.Exports;

/// <summary>
/// The export rules: one test per rule in the brief's "Export rules".
/// </summary>
public sealed class ExportServiceTests
{
    private static readonly DateTimeOffset ExportedAt =
        new(2026, 3, 1, 23, 30, 0, TimeSpan.Zero);

    private readonly FakeArchive archive = new();

    [Fact]
    public async Task A_document_with_one_part_becomes_a_file_named_after_the_document()
    {
        archive.Add("invoice-001.txt", "scan.pdf");

        Assert.Equal(["invoice-001.txt"], (await Entries()).Keys);
    }

    [Fact]
    public async Task A_document_with_several_parts_becomes_a_folder_with_one_file_per_part()
    {
        archive.Add("delivery-2026-03", "page1.txt", "page2.txt");

        Assert.Equal(
            ["delivery-2026-03/page1.txt", "delivery-2026-03/page2.txt"],
            (await Entries()).Keys);
    }

    [Fact]
    public async Task A_document_with_no_parts_becomes_an_empty_folder()
    {
        archive.Add("empty-folder");

        var entries = await Entries();

        Assert.Equal(["empty-folder/"], entries.Keys);
        Assert.Empty(entries["empty-folder/"]);
    }

    [Fact]
    public async Task Parts_keep_the_order_the_Archive_stored_them_in()
    {
        archive.Add("delivery", "page1.txt", "page10.txt", "page2.txt");

        Assert.Equal(
            ["delivery/page1.txt", "delivery/page10.txt", "delivery/page2.txt"],
            (await Entries()).Keys);
    }

    [Fact]
    public async Task Part_content_is_copied_byte_for_byte()
    {
        archive.Add("invoice-001.txt", "scan.pdf");

        Assert.Equal(
            "content of invoice-001.txt/scan.pdf",
            Text((await Entries())["invoice-001.txt"]));
    }

    [Fact]
    public async Task Two_documents_with_the_same_title_both_survive()
    {
        archive.Add("invoice-001.txt", "a.pdf");
        archive.Add("invoice-001.txt", "b.pdf");

        var entries = await Entries();

        Assert.Equal(["invoice-001.txt", "invoice-001 (2).txt"], entries.Keys);
        Assert.Equal("content of invoice-001.txt/a.pdf", Text(entries["invoice-001.txt"]));
        Assert.Equal("content of invoice-001.txt/b.pdf", Text(entries["invoice-001 (2).txt"]));
    }

    [Fact]
    public async Task A_third_document_with_the_same_title_is_numbered_on()
    {
        archive.Add("invoice.txt", "a.pdf");
        archive.Add("invoice.txt", "b.pdf");
        archive.Add("invoice.txt", "c.pdf");

        Assert.Equal(
            ["invoice.txt", "invoice (2).txt", "invoice (3).txt"],
            (await Entries()).Keys);
    }

    [Fact]
    public async Task A_duplicate_folder_title_is_numbered_at_the_end()
    {
        archive.Add("delivery", "page1.txt", "page2.txt");
        archive.Add("delivery", "page1.txt", "page2.txt");

        Assert.Equal(
            ["delivery/page1.txt", "delivery/page2.txt",
                "delivery (2)/page1.txt", "delivery (2)/page2.txt"],
            (await Entries()).Keys);
    }

    [Fact]
    public async Task A_one_part_document_is_a_file_named_after_the_document_not_after_the_part()
    {
        archive.Add("delivery", "page1.txt");
        archive.Add("delivery", "page1.txt");

        Assert.Equal(["delivery", "delivery (2)"], (await Entries()).Keys);
    }

    [Fact]
    public async Task Titles_that_differ_only_by_case_are_kept_apart()
    {
        archive.Add("Invoice.txt", "a.pdf");
        archive.Add("invoice.txt", "b.pdf");

        Assert.Equal(["Invoice.txt", "invoice (2).txt"], (await Entries()).Keys);
    }

    [Fact]
    public async Task Two_parts_of_one_document_sharing_a_name_both_survive()
    {
        archive.Add("delivery", "page1.txt", "page1.txt");

        Assert.Equal(
            ["delivery/page1.txt", "delivery/page1 (2).txt"],
            (await Entries()).Keys);
    }

    [Fact]
    public async Task A_unicode_title_is_kept_exactly()
    {
        archive.Add("Договор София.txt", "scan.pdf");

        Assert.Equal(["Договор София.txt"], (await Entries()).Keys);
    }

    [Fact]
    public async Task Every_exported_document_is_marked_with_the_export_date()
    {
        var first = archive.Add("a.txt", "a.pdf");
        var second = archive.Add("b.txt", "b.pdf");

        await Run();

        Assert.Equal(
            [(first.Id, "Exported on 2026-03-01"), (second.Id, "Exported on 2026-03-01")],
            archive.Marked.Select(mark => (mark.DocumentId, mark.Value)));
    }

    [Fact]
    public async Task The_marker_is_written_into_the_field_the_caller_chose()
    {
        archive.Add("a.txt", "a.pdf");

        await Run(markerField: "Status");

        Assert.Equal("Status", Assert.Single(archive.Marked).Field);
    }

    [Fact]
    public async Task The_markers_date_is_the_export_date_in_UTC()
    {
        // 23:30 UTC on the 1st is the 1st, whatever the caller's own clock says. There is no time
        // zone to read it in any more — see "Removed: the time zone parameter" in DECISIONS.md.
        archive.Add("a.txt", "a.pdf");

        await Run();

        Assert.Equal("Exported on 2026-03-01", Assert.Single(archive.Marked).Value);
    }

    [Fact]
    public async Task The_marker_records_rather_than_filters()
    {
        archive.Add("a.txt", "a.pdf");

        await Run();
        await Run();

        Assert.Equal(2, archive.Marked.Count);
        Assert.All(archive.Marked, mark => Assert.Equal("Exported on 2026-03-01", mark.Value));
    }

    [Fact]
    public async Task Nothing_is_marked_until_the_whole_archive_is_built()
    {
        // The marker claims a document was exported, so it must not be written for one the caller
        // never receives. Marking once at the end makes that true of the whole export rather than
        // of each document in turn: a failure partway leaves nothing marked.
        var failing = new FakeArchive { FailOpeningPart = new IOException("the part is gone") };
        failing.Add("first.txt", "a.pdf");
        failing.Add("second.txt", "b.pdf");

        await Assert.ThrowsAsync<IOException>(() => Run(failing));

        Assert.Empty(failing.Marked);
    }

    [Fact]
    public async Task Every_document_is_marked_in_a_single_request()
    {
        archive.Add("a.txt", "a.pdf");
        archive.Add("b.txt", "b.pdf");
        archive.Add("c.txt", "c.pdf");

        await Run();

        Assert.Equal(3, archive.Marked.Count);
        Assert.Equal(1, archive.MarkRequests);
    }

    [Fact]
    public async Task An_export_that_matches_nothing_asks_the_Archive_to_mark_nothing()
    {
        await Run();

        Assert.Empty(archive.Marked);
        Assert.Equal(0, archive.MarkRequests);
    }

    [Fact]
    public async Task Every_document_in_the_window_is_written_and_marked_once()
    {
        archive.Add("one.txt", "a.pdf");
        archive.Add("folder", "a.pdf", "b.pdf");
        archive.Add("empty");

        var entries = await Entries();

        Assert.Equal(["one.txt", "folder/a.pdf", "folder/b.pdf", "empty/"], entries.Keys);
        Assert.Equal(3, archive.Marked.Count);
    }

    [Fact]
    public async Task The_archive_comes_back_readable_from_the_start()
    {
        archive.Add("a.txt", "a.pdf");

        await using var written = await Service(archive).WriteAsync(
            Request("Batch"), CancellationToken.None);

        Assert.True(written.CanRead);
        Assert.Equal(0, written.Position);
        Assert.True(written.Length > 0);
    }

    [Fact]
    public async Task A_failed_export_leaves_no_file_behind()
    {
        // The stream is what deletes the temp file when it closes, and a failed export returns no
        // stream — so the service has to close it itself.
        var failing = new FakeArchive { FailOpeningPart = new IOException("the part is gone") };
        failing.Add("a.txt", "a.pdf");
        var before = Directory.GetFiles(Path.GetTempPath(), "export-*.zip").Length;

        await Assert.ThrowsAsync<IOException>(() => Run(failing));

        Assert.Equal(before, Directory.GetFiles(Path.GetTempPath(), "export-*.zip").Length);
    }

    [Fact]
    public async Task The_cabinet_and_the_dates_are_asked_of_the_Archive_as_given()
    {
        await Run();

        Assert.Equal(
            ("Invoices", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
            archive.AskedFor);
    }

    [Fact]
    public async Task An_export_that_matches_nothing_is_an_empty_archive_rather_than_a_failure()
    {
        var entries = await Entries();

        Assert.Empty(entries);
        Assert.Empty(archive.Marked);
    }

    [Fact]
    public async Task Cancellation_stops_the_export()
    {
        archive.Add("a.txt", "a.pdf");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Run(token: cancelled.Token));
    }

    /// <summary>Runs an export and throws the archive away.</summary>
    private async Task Run(
        FakeArchive? client = null,
        string markerField = "Batch",
        CancellationToken token = default)
    {
        await using var archiveStream = await Service(client ?? archive)
            .WriteAsync(Request(markerField), token);
    }

    /// <summary>Runs an export and reads the archive it returned, entry by entry.</summary>
    private async Task<Dictionary<string, byte[]>> Entries(string markerField = "Batch")
    {
        await using var archiveStream = await Service(archive).WriteAsync(
            Request(markerField), CancellationToken.None);

        using var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read);

        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            using var content = entry.Open();
            using var bytes = new MemoryStream();
            content.CopyTo(bytes);
            entries[entry.FullName] = bytes.ToArray();
        }

        return entries;
    }

    private static ExportService Service(FakeArchive client) =>
        new(client, new FixedClock(ExportedAt), NullLogger<ExportService>.Instance);

    private static ExportRequest Request(string markerField) =>
        new("Invoices", markerField, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

    private static string Text(byte[] content) => Encoding.UTF8.GetString(content);

    /// <summary>A clock that does not move, so the marker's date is not timing-dependent.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

}
