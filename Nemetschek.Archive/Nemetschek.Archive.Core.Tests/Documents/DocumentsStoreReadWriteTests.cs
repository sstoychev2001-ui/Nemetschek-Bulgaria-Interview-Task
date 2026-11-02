using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Exceptions.Documents;
using Nemetschek.Archive.Core.Services.Documents;
using Nemetschek.Archive.Core.Utils.Documents;
using Nemetschek.Archive.Core.Validation.Documents;

namespace Nemetschek.Archive.Core.Tests.Documents;

/// <summary>
/// Finding one document, reading a part's content, filtering by date, and changing index values.
/// </summary>
public sealed class DocumentsStoreReadWriteTests : IDisposable
{
    private readonly string root =
        Path.Combine(Path.GetTempPath(), $"archive-rw-{Guid.NewGuid():N}");

    private readonly FixedClock clock = new(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));

    private readonly DocumentsStoreService store;

    public DocumentsStoreReadWriteTests()
    {
        Directory.CreateDirectory(DocumentStorageUtils.Staging(root));
        store = new DocumentsStoreService(
            root, clock, NullLogger<DocumentsStoreService>.Instance);
    }

    [Fact]
    public async Task A_stored_document_is_found_by_its_id()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        var found = await store.FindAsync("Invoices", stored.Id, CancellationToken.None);

        Assert.Equal(stored.Id, found.Id);
        Assert.Equal("invoice-001.txt", found.Title);
    }

    [Fact]
    public async Task An_id_the_cabinet_does_not_hold_is_refused()
    {
        await Add("invoice-001.txt", "page1.txt");
        var missing = Guid.CreateVersion7();

        var refusal = await Assert.ThrowsAsync<DocumentNotFoundException>(
            () => store.FindAsync("Invoices", missing, CancellationToken.None));

        Assert.Equal(missing, refusal.DocumentId);
    }

    [Fact]
    public async Task A_document_is_not_reachable_through_another_cabinet()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await Assert.ThrowsAsync<DocumentNotFoundException>(
            () => store.FindAsync("Contracts", stored.Id, CancellationToken.None));
    }

    [Fact]
    public async Task A_parts_content_is_read_back_byte_for_byte()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await using var content = await store.OpenPartAsync(
            "Invoices", stored.Id, stored.Parts[0].Id, CancellationToken.None);

        Assert.Equal("content of page1.txt", await new StreamReader(content).ReadToEndAsync());
    }

    [Fact]
    public async Task The_right_part_of_a_many_part_document_is_read()
    {
        var stored = await Add("delivery", "page1.txt", "page2.txt");

        await using var content = await store.OpenPartAsync(
            "Invoices", stored.Id, stored.Parts[1].Id, CancellationToken.None);

        Assert.Equal("content of page2.txt", await new StreamReader(content).ReadToEndAsync());
    }

    [Fact]
    public async Task A_part_id_the_document_does_not_have_is_refused()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await Assert.ThrowsAsync<DocumentNotFoundException>(
            () => store.OpenPartAsync(
                "Invoices", stored.Id, Guid.CreateVersion7(), CancellationToken.None));
    }

    [Fact]
    public async Task A_part_of_a_document_that_is_not_there_is_refused()
    {
        await Assert.ThrowsAsync<DocumentNotFoundException>(
            () => store.OpenPartAsync(
                "Invoices", Guid.CreateVersion7(), Guid.CreateVersion7(), CancellationToken.None));
    }

    [Fact]
    public async Task An_index_value_is_set_and_read_back()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await store.SetIndexValuesAsync(
            "Invoices",
            stored.Id,
            new Dictionary<string, IndexValue> { ["Batch"] = new IndexValue.Text("Exported on 2026-03-01") },
            CancellationToken.None);

        var reread = await store.FindAsync("Invoices", stored.Id, CancellationToken.None);
        Assert.Equal(
            "Exported on 2026-03-01",
            Assert.IsType<IndexValue.Text>(reread.IndexValues["Batch"]).Value);
    }

    [Fact]
    public async Task Setting_one_value_leaves_the_others_alone()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await store.SetIndexValuesAsync(
            "Invoices",
            stored.Id,
            new Dictionary<string, IndexValue> { ["Status"] = new IndexValue.Text("Done") },
            CancellationToken.None);

        var reread = await store.FindAsync("Invoices", stored.Id, CancellationToken.None);
        Assert.Equal("sample-material", Assert.IsType<IndexValue.Text>(reread.IndexValues["Batch"]).Value);
        Assert.Equal("Done", Assert.IsType<IndexValue.Text>(reread.IndexValues["Status"]).Value);
    }

    [Fact]
    public async Task A_value_is_matched_to_its_field_whatever_case_it_is_given_in()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await store.SetIndexValuesAsync(
            "Invoices",
            stored.Id,
            new Dictionary<string, IndexValue> { ["batch"] = new IndexValue.Text("again") },
            CancellationToken.None);

        var reread = await store.FindAsync("Invoices", stored.Id, CancellationToken.None);
        Assert.Equal("again", Assert.IsType<IndexValue.Text>(reread.IndexValues["Batch"]).Value);
        Assert.Single(reread.IndexValues);
    }

    [Fact]
    public async Task Changing_a_document_moves_its_modified_time_but_not_when_it_was_stored()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");
        clock.Now = clock.Now.AddDays(5);

        await store.SetIndexValuesAsync(
            "Invoices",
            stored.Id,
            new Dictionary<string, IndexValue> { ["Batch"] = new IndexValue.Text("later") },
            CancellationToken.None);

        var reread = await store.FindAsync("Invoices", stored.Id, CancellationToken.None);
        Assert.Equal(stored.StoredAt, reread.StoredAt);
        Assert.Equal(clock.Now, reread.ModifiedAt);
    }

    [Fact]
    public async Task A_change_leaves_the_parts_and_their_content_untouched()
    {
        var stored = await Add("delivery", "page1.txt", "page2.txt");

        await store.SetIndexValuesAsync(
            "Invoices",
            stored.Id,
            new Dictionary<string, IndexValue> { ["Batch"] = new IndexValue.Text("later") },
            CancellationToken.None);

        var reread = await store.FindAsync("Invoices", stored.Id, CancellationToken.None);
        Assert.Equal(["page1.txt", "page2.txt"], reread.Parts.Select(part => part.Name));

        await using var content = await store.OpenPartAsync(
            "Invoices", stored.Id, stored.Parts[0].Id, CancellationToken.None);
        Assert.Equal("content of page1.txt", await new StreamReader(content).ReadToEndAsync());
    }

    [Fact]
    public async Task A_change_leaves_nothing_staged()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await store.SetIndexValuesAsync(
            "Invoices",
            stored.Id,
            new Dictionary<string, IndexValue> { ["Batch"] = new IndexValue.Text("later") },
            CancellationToken.None);

        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "tmp")));
    }

    [Fact]
    public async Task Changing_a_document_that_is_not_there_is_refused_and_changes_nothing()
    {
        var untouched = await Add("invoice-001.txt", "page1.txt");

        await Assert.ThrowsAsync<DocumentNotFoundException>(() => store.SetIndexValuesAsync(
            "Invoices",
            Guid.CreateVersion7(),
            new Dictionary<string, IndexValue> { ["Batch"] = new IndexValue.Text("later") },
            CancellationToken.None));

        var reread = await store.FindAsync("Invoices", untouched.Id, CancellationToken.None);
        Assert.Equal(
            "sample-material",
            Assert.IsType<IndexValue.Text>(reread.IndexValues["Batch"]).Value);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "tmp")));
    }

    [Fact]
    public async Task A_page_holds_only_the_documents_stored_inside_the_window()
    {
        await AddOn(new DateOnly(2026, 2, 27), "february.txt");
        await AddOn(new DateOnly(2026, 3, 1), "march-first.txt");
        await AddOn(new DateOnly(2026, 3, 2), "march-second.txt");

        var march = CalendarRange.Of(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1));

        var page = await store.ListAsync("Invoices", march, 0, 10, CancellationToken.None);

        Assert.Equal(["march-first.txt"], page.Documents.Select(document => document.Title));
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task The_total_counts_only_what_the_window_holds()
    {
        await AddOn(new DateOnly(2026, 3, 1), "a.txt");
        await AddOn(new DateOnly(2026, 3, 1), "b.txt");
        await AddOn(new DateOnly(2026, 4, 1), "c.txt");

        var march = CalendarRange.Of(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var page = await store.ListAsync("Invoices", march, 0, 1, CancellationToken.None);

        Assert.Equal(2, page.Total);
        Assert.Single(page.Documents);
    }

    [Fact]
    public async Task A_window_that_matches_nothing_is_an_empty_page()
    {
        await AddOn(new DateOnly(2026, 3, 1), "a.txt");

        var page = await store.ListAsync(
            "Invoices",
            CalendarRange.Of(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)),
            0,
            10,
            CancellationToken.None);

        Assert.Empty(page.Documents);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task A_document_belongs_to_the_UTC_day_it_was_stored_on()
    {
        clock.Now = new DateTimeOffset(2026, 2, 28, 23, 0, 0, TimeSpan.Zero);
        await Add("late-february.txt", "page1.txt");

        var firstOfMarch = CalendarRange.Of(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1));
        var lastOfFebruary = CalendarRange.Of(new DateOnly(2026, 2, 28), new DateOnly(2026, 2, 28));

        Assert.Empty(
            (await store.ListAsync("Invoices", firstOfMarch, 0, 10, CancellationToken.None)).Documents);
        Assert.Single(
            (await store.ListAsync("Invoices", lastOfFebruary, 0, 10, CancellationToken.None)).Documents);
    }

    [Fact]
    public async Task A_window_that_starts_inside_a_day_excludes_what_was_stored_before_it()
    {
        clock.Now = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);
        await Add("morning.txt", "page1.txt");
        clock.Now = new DateTimeOffset(2026, 3, 1, 15, 0, 0, TimeSpan.Zero);
        await Add("afternoon.txt", "page1.txt");

        var afternoon = new StoredRange(
            new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero));

        var page = await store.ListAsync("Invoices", afternoon, 0, 10, CancellationToken.None);

        Assert.Equal(["afternoon.txt"], page.Documents.Select(document => document.Title));
        Assert.Equal(1, page.Total);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private Task<Document> Add(string title, params string[] partNames) =>
        store.AddAsync(
            new Document
            {
                CabinetName = "Invoices",
                Title = title,
                IndexValues = new Dictionary<string, IndexValue>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Batch"] = new IndexValue.Text("sample-material"),
                },
                Parts =
                [
                    .. partNames.Select(name => Part.ToStore(
                        name,
                        () => new MemoryStream(Encoding.UTF8.GetBytes($"content of {name}")))),
                ],
            },
            CancellationToken.None);

    private Task<Document> AddOn(DateOnly day, string title)
    {
        clock.Now = new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

        return Add(title, "page1.txt");
    }

    /// <summary>A clock the test moves on purpose.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
