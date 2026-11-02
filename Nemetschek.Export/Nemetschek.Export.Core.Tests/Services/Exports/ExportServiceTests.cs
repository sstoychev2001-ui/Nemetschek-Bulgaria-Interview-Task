using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nemetschek.Export.Core.Clients.Interfaces;
using Nemetschek.Export.Core.Domain.Exports;
using Nemetschek.Export.Core.Services.Exports;

namespace Nemetschek.Export.Core.Tests.Services.Exports;

/// <summary>
/// The export rules: one test per rule in the brief's "Export rules". The Archive is a mock that
/// answers from the documents a test adds, and records what it was asked to mark.
/// </summary>
[TestClass]
public sealed class ExportServiceTests
{
    private static readonly DateTimeOffset ExportedAt =
        new(2026, 3, 1, 23, 30, 0, TimeSpan.Zero);

    private readonly List<ArchiveDocument> documents = [];
    private readonly Dictionary<Guid, string> content = [];

    private readonly List<(string Cabinet, Guid DocumentId, string Field, string Value)> marked = [];
    private Mock<IArchiveClient> archive = null!;
    private ExportService service = null!;

    [TestInitialize]
    public void BeforeEach()
    {
        archive = new Mock<IArchiveClient>();

        archive
            .Setup(client => client.ReadDocumentsAsync(
                It.IsAny<string>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .Returns((string _, DateOnly _, DateOnly _, CancellationToken token) => Stored(token));

        ReturnPartContent();

        archive
            .Setup(client => client.SetTextIndexValueAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback((
                    string cabinet,
                    IReadOnlyCollection<Guid> documentIds,
                    string field,
                    string value,
                    CancellationToken _) =>
                marked.AddRange(documentIds.Select(id => (cabinet, id, field, value))))
            .Returns(Task.CompletedTask);

        service = new ExportService(
            archive.Object, new FixedClock(ExportedAt), NullLogger<ExportService>.Instance);
    }

    [TestMethod]
    public async Task A_document_with_one_part_becomes_a_file_named_after_the_document()
    {
        Add("invoice-001.txt", "scan.pdf");

        CollectionAssert.AreEqual(new[] { "invoice-001.txt" }, (await Entries()).Keys.ToArray());
    }

    [TestMethod]
    public async Task A_document_with_several_parts_becomes_a_folder_with_one_file_per_part()
    {
        Add("delivery-2026-03", "page1.txt", "page2.txt");

        CollectionAssert.AreEqual(
            new[] { "delivery-2026-03/page1.txt", "delivery-2026-03/page2.txt" },
            (await Entries()).Keys.ToArray());
    }

    [TestMethod]
    public async Task A_document_with_no_parts_becomes_an_empty_folder()
    {
        Add("empty-folder");

        var entries = await Entries();

        CollectionAssert.AreEqual(new[] { "empty-folder/" }, entries.Keys.ToArray());
        Assert.IsEmpty(entries["empty-folder/"]);
    }

    [TestMethod]
    public async Task Parts_keep_the_order_the_Archive_stored_them_in()
    {
        Add("delivery", "page1.txt", "page10.txt", "page2.txt");

        CollectionAssert.AreEqual(
            new[] { "delivery/page1.txt", "delivery/page10.txt", "delivery/page2.txt" },
            (await Entries()).Keys.ToArray());
    }

    [TestMethod]
    public async Task Part_content_is_copied_byte_for_byte()
    {
        Add("invoice-001.txt", "scan.pdf");

        Assert.AreEqual(
            "content of invoice-001.txt/scan.pdf",
            Text((await Entries())["invoice-001.txt"]));
    }

    [TestMethod]
    public async Task Two_documents_with_the_same_title_both_survive()
    {
        Add("invoice-001.txt", "a.pdf");
        Add("invoice-001.txt", "b.pdf");

        var entries = await Entries();

        CollectionAssert.AreEqual(
            new[] { "invoice-001.txt", "invoice-001 (2).txt" },
            entries.Keys.ToArray());
        Assert.AreEqual("content of invoice-001.txt/a.pdf", Text(entries["invoice-001.txt"]));
        Assert.AreEqual("content of invoice-001.txt/b.pdf", Text(entries["invoice-001 (2).txt"]));
    }

    [TestMethod]
    public async Task A_third_document_with_the_same_title_is_numbered_on()
    {
        Add("invoice.txt", "a.pdf");
        Add("invoice.txt", "b.pdf");
        Add("invoice.txt", "c.pdf");

        CollectionAssert.AreEqual(
            new[] { "invoice.txt", "invoice (2).txt", "invoice (3).txt" },
            (await Entries()).Keys.ToArray());
    }

    [TestMethod]
    public async Task A_duplicate_folder_title_is_numbered_at_the_end()
    {
        Add("delivery", "page1.txt", "page2.txt");
        Add("delivery", "page1.txt", "page2.txt");

        CollectionAssert.AreEqual(
            new[] { "delivery/page1.txt", "delivery/page2.txt",
                "delivery (2)/page1.txt", "delivery (2)/page2.txt" },
            (await Entries()).Keys.ToArray());
    }

    [TestMethod]
    public async Task A_one_part_document_is_a_file_named_after_the_document_not_after_the_part()
    {
        Add("delivery", "page1.txt");
        Add("delivery", "page1.txt");

        CollectionAssert.AreEqual(
            new[] { "delivery", "delivery (2)" },
            (await Entries()).Keys.ToArray());
    }

    [TestMethod]
    public async Task Titles_that_differ_only_by_case_are_kept_apart()
    {
        Add("Invoice.txt", "a.pdf");
        Add("invoice.txt", "b.pdf");

        CollectionAssert.AreEqual(
            new[] { "Invoice.txt", "invoice (2).txt" },
            (await Entries()).Keys.ToArray());
    }

    [TestMethod]
    public async Task Two_parts_of_one_document_sharing_a_name_both_survive()
    {
        Add("delivery", "page1.txt", "page1.txt");

        CollectionAssert.AreEqual(
            new[] { "delivery/page1.txt", "delivery/page1 (2).txt" },
            (await Entries()).Keys.ToArray());
    }

    [TestMethod]
    public async Task A_unicode_title_is_kept_exactly()
    {
        Add("Договор София.txt", "scan.pdf");

        CollectionAssert.AreEqual(new[] { "Договор София.txt" }, (await Entries()).Keys.ToArray());
    }

    [TestMethod]
    public async Task Every_exported_document_is_marked_with_the_export_date()
    {
        var first = Add("a.txt", "a.pdf");
        var second = Add("b.txt", "b.pdf");

        await Run();

        CollectionAssert.AreEqual(
            new[] { (first.Id, "Exported on 2026-03-01"), (second.Id, "Exported on 2026-03-01") },
            marked.Select(mark => (mark.DocumentId, mark.Value)).ToArray());
    }

    [TestMethod]
    public async Task The_marker_is_written_into_the_field_the_caller_chose()
    {
        Add("a.txt", "a.pdf");

        await Run(markerField: "Status");

        Assert.AreEqual("Status", Assert.ContainsSingle(marked).Field);
    }

    [TestMethod]
    public async Task The_markers_date_is_the_export_date_in_UTC()
    {
        Add("a.txt", "a.pdf");

        await Run();

        Assert.AreEqual("Exported on 2026-03-01", Assert.ContainsSingle(marked).Value);
    }

    [TestMethod]
    public async Task The_marker_records_rather_than_filters()
    {
        Add("a.txt", "a.pdf");

        await Run();
        await Run();

        Assert.AreEqual(2, marked.Count);
        Assert.IsEmpty(marked.Where(mark => mark.Value != "Exported on 2026-03-01"));
    }

    [TestMethod]
    public async Task Nothing_is_marked_until_the_whole_archive_is_built()
    {
        Add("first.txt", "a.pdf");
        Add("second.txt", "b.pdf");
        FailOpeningParts(new IOException("the part is gone"));

        await Assert.ThrowsExactlyAsync<IOException>(() => Run());

        Assert.IsEmpty(marked);
    }

    [TestMethod]
    public async Task Every_document_is_marked_in_a_single_request()
    {
        Add("a.txt", "a.pdf");
        Add("b.txt", "b.pdf");
        Add("c.txt", "c.pdf");

        await Run();

        Assert.AreEqual(3, marked.Count);
        VerifyMarkRequests(Times.Once());
    }

    [TestMethod]
    public async Task An_export_that_matches_nothing_asks_the_Archive_to_mark_nothing()
    {
        await Run();

        Assert.IsEmpty(marked);
        VerifyMarkRequests(Times.Never());
    }

    [TestMethod]
    public async Task Every_document_in_the_window_is_written_and_marked_once()
    {
        Add("one.txt", "a.pdf");
        Add("folder", "a.pdf", "b.pdf");
        Add("empty");

        var entries = await Entries();

        CollectionAssert.AreEqual(
            new[] { "one.txt", "folder/a.pdf", "folder/b.pdf", "empty/" },
            entries.Keys.ToArray());
        Assert.AreEqual(3, marked.Count);
    }

    [TestMethod]
    public async Task The_archive_comes_back_readable_from_the_start()
    {
        Add("a.txt", "a.pdf");

        await using var written = await service.WriteAsync(
            Request("Batch"), CancellationToken.None);

        Assert.IsTrue(written.CanRead);
        Assert.AreEqual(0, written.Position);
        Assert.IsTrue(written.Length > 0);
    }

    [TestMethod]
    public async Task A_failed_export_leaves_no_file_behind()
    {
        Add("a.txt", "a.pdf");
        FailOpeningParts(new IOException("the part is gone"));
        var before = Directory.GetFiles(Path.GetTempPath(), "export-*.zip").Length;

        await Assert.ThrowsExactlyAsync<IOException>(() => Run());

        Assert.AreEqual(before, Directory.GetFiles(Path.GetTempPath(), "export-*.zip").Length);
    }

    [TestMethod]
    public async Task The_cabinet_and_the_dates_are_asked_of_the_Archive_as_given()
    {
        await Run();

        archive.Verify(
            client => client.ReadDocumentsAsync(
                "Invoices",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task An_export_that_matches_nothing_is_an_empty_archive_rather_than_a_failure()
    {
        var entries = await Entries();

        Assert.IsEmpty(entries);
        Assert.IsEmpty(marked);
    }

    [TestMethod]
    public async Task Cancellation_stops_the_export()
    {
        Add("a.txt", "a.pdf");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Run(token: cancelled.Token));
    }

    private ArchiveDocument Add(string title, params string[] partNames)
    {
        var parts = partNames
            .Select(name =>
            {
                var part = new ArchivePart(Guid.CreateVersion7(), name);
                content[part.Id] = $"content of {title}/{name}";

                return part;
            })
            .ToList();

        var document = new ArchiveDocument(Guid.CreateVersion7(), title, parts);
        documents.Add(document);

        return document;
    }

    private async IAsyncEnumerable<ArchiveDocument> Stored(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return document;

            await Task.Yield();
        }
    }

    private void ReturnPartContent() =>
        archive
            .Setup(client => client.OpenPartAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, Guid _, Guid partId, CancellationToken _) =>
                (Stream)new MemoryStream(Encoding.UTF8.GetBytes(content[partId])));

    private void FailOpeningParts(Exception failure) =>
        archive
            .Setup(client => client.OpenPartAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

    private void VerifyMarkRequests(Times times) =>
        archive.Verify(
            client => client.SetTextIndexValueAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            times);

    private async Task Run(string markerField = "Batch", CancellationToken token = default)
    {
        await using var archiveStream = await service.WriteAsync(Request(markerField), token);
    }

    private async Task<Dictionary<string, byte[]>> Entries(string markerField = "Batch")
    {
        await using var archiveStream = await service.WriteAsync(
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

    private static ExportRequest Request(string markerField) =>
        new("Invoices", markerField, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

    private static string Text(byte[] content) => Encoding.UTF8.GetString(content);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
