using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Exceptions.Documents;
using Nemetschek.Archive.Core.Services.Documents;
using Nemetschek.Archive.Core.Utils.Documents;
using Nemetschek.Archive.Core.Validation.Documents;

namespace Nemetschek.Archive.Core.Tests.Services.Documents;

/// <summary>
/// The store: what it does, what it writes down, and what it reads back — storing, finding one
/// document, a part's content, the date window, and changing index values. Runs against a throwaway
/// directory with a clock the test moves: no container, no network, nothing timing-dependent.
/// </summary>
[TestClass]
public sealed class DocumentsStoreServiceTests
{
    private readonly string root =
        Path.Combine(Path.GetTempPath(), $"archive-store-{Guid.NewGuid():N}");

    private readonly FixedClock clock = new(new DateTimeOffset(2026, 3, 1, 9, 30, 0, TimeSpan.Zero));

    private readonly List<(LogLevel Level, string Message, Exception? Exception)> logged = [];

    /// <summary>The store under test, on a prepared, empty storage root.</summary>
    private DocumentsStoreService store = null!;

    [TestInitialize]
    public void BeforeEach()
    {
        var logger = new Mock<ILogger<DocumentsStoreService>>();
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        logger
            .Setup(log => log.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            // The state is the framework's FormattedLogValues, whose ToString() is the rendered
            // message — the same string the formatter argument would produce.
            .Callback(new InvocationAction(invocation => logged.Add((
                (LogLevel)invocation.Arguments[0],
                invocation.Arguments[2]?.ToString() ?? string.Empty,
                (Exception?)invocation.Arguments[3]))));

        Directory.CreateDirectory(DocumentStorageUtils.Staging(root));
        store = new DocumentsStoreService(root, clock, logger.Object);
    }

    [TestMethod]
    public async Task A_stored_document_is_logged_with_its_id()
    {
        var stored = await store.AddAsync(
            Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);

        var entry = Assert.ContainsSingle(logged);
        Assert.AreEqual(LogLevel.Information, entry.Level);
        Assert.Contains(stored.Id.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Contains("Invoices", entry.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task A_store_failure_is_logged_with_its_exception_and_rethrown()
    {
        var failure = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => store.AddAsync(Draft(new Part { Name = "page1.txt" }), CancellationToken.None));

        var entry = Assert.ContainsSingle(logged);
        Assert.AreEqual(LogLevel.Error, entry.Level);
        Assert.AreSame(failure, entry.Exception);
    }

    [TestMethod]
    public async Task A_cancelled_store_is_logged_at_Error_like_any_other_failure()
    {
        var draft = Draft(Part.ToStore("page1.txt", () => throw new OperationCanceledException()));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => store.AddAsync(draft, CancellationToken.None));

        var entry = Assert.ContainsSingle(logged);
        Assert.AreEqual(LogLevel.Error, entry.Level);
        Assert.IsInstanceOfType<OperationCanceledException>(entry.Exception);
    }

    [TestMethod]
    public async Task Reading_a_page_logs_nothing()
    {
        await store.ListAsync("Invoices", StoredRange.All, skip: 0, take: 10, CancellationToken.None);

        Assert.IsEmpty(logged);
    }

    [TestMethod]
    public async Task A_stored_document_comes_back_from_disk_as_it_went_in()
    {
        await store.AddAsync(Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);

        var page = await store.ListAsync("Invoices", StoredRange.All, 0, 10, CancellationToken.None);

        var document = Assert.ContainsSingle(page.Documents);
        Assert.AreEqual("invoice-001.txt", document.Title);
        Assert.AreEqual("Invoices", document.CabinetName);
        Assert.IsInstanceOfType<IndexValue.Text>(document.IndexValues["Batch"], out var batch);
        Assert.AreEqual("sample-material", batch.Value);
        var part = Assert.ContainsSingle(document.Parts);
        Assert.AreEqual("page1.txt", part.Name);
        Assert.AreEqual(0, part.Order);
        Assert.AreEqual(8, part.SizeInBytes);
    }

    [TestMethod]
    public async Task Part_content_is_stored_byte_for_byte()
    {
        await store.AddAsync(Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);

        var file = Assert.ContainsSingle(
            Directory.EnumerateFiles(root, "*.bin", SearchOption.AllDirectories));
        Assert.AreEqual("page one", await File.ReadAllTextAsync(file, TestToken));
    }

    [TestMethod]
    public async Task The_store_sets_the_system_fields_and_ignores_what_arrived()
    {
        var arrived = Draft(Part.ToStore("page1.txt", Content)) with
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            StoredAt = DateTimeOffset.UnixEpoch,
            ModifiedAt = DateTimeOffset.UnixEpoch,
        };

        var stored = await store.AddAsync(arrived, CancellationToken.None);

        Assert.AreNotEqual(arrived.Id, stored.Id);
        Assert.AreEqual(clock.GetUtcNow(), stored.StoredAt);
        Assert.AreEqual(clock.GetUtcNow(), stored.ModifiedAt);
    }

    [TestMethod]
    public async Task An_id_carries_the_time_it_was_stored()
    {
        var stored = await store.AddAsync(
            Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);

        Assert.AreEqual(7, stored.Id.Version);
    }

    [TestMethod]
    public async Task The_content_callback_is_cleared_once_the_part_is_stored()
    {
        var stored = await store.AddAsync(
            Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);

        Assert.IsNull(Assert.ContainsSingle(stored.Parts).OpenContent);
    }

    [TestMethod]
    public async Task A_users_name_never_becomes_a_file_name()
    {
        var stored = await store.AddAsync(
            Draft(Part.ToStore("Договор София.txt", Content)), CancellationToken.None);

        var partFile = Assert.ContainsSingle(
            Directory.EnumerateFiles(root, "*.bin", SearchOption.AllDirectories));
        Assert.Contains(
            Assert.ContainsSingle(stored.Parts).Id.ToString(),
            Path.GetFileName(partFile),
            StringComparison.Ordinal);
        Assert.DoesNotContain("Договор", partFile, StringComparison.Ordinal);

        var page = await store.ListAsync("Invoices", StoredRange.All, 0, 10, CancellationToken.None);
        Assert.AreEqual("Договор София.txt", Assert.ContainsSingle(page.Documents).Parts[0].Name);
    }

    [TestMethod]
    public async Task A_document_with_no_parts_is_stored()
    {
        await store.AddAsync(
            new Document
            {
                CabinetName = "Invoices",
                Title = "empty-folder",
                IndexValues = Values(),
                Parts = [],
            },
            CancellationToken.None);

        var page = await store.ListAsync("Invoices", StoredRange.All, 0, 10, CancellationToken.None);

        Assert.IsEmpty(Assert.ContainsSingle(page.Documents).Parts);
    }

    [TestMethod]
    public async Task A_zero_byte_part_is_stored_with_a_size_of_zero()
    {
        var stored = await store.AddAsync(
            Draft(Part.ToStore("empty-note.txt", () => new MemoryStream())),
            CancellationToken.None);

        Assert.AreEqual(0, Assert.ContainsSingle(stored.Parts).SizeInBytes);
    }

    [TestMethod]
    public async Task Parts_keep_the_order_they_were_given()
    {
        await store.AddAsync(
            new Document
            {
                CabinetName = "Invoices",
                Title = "delivery",
                IndexValues = Values(),
                Parts =
                [
                    Part.ToStore("page1.txt", Content),
                    Part.ToStore("page10.txt", Content),
                    Part.ToStore("page2.txt", Content),
                ],
            },
            CancellationToken.None);

        var page = await store.ListAsync("Invoices", StoredRange.All, 0, 10, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "page1.txt", "page10.txt", "page2.txt" },
            Assert.ContainsSingle(page.Documents).Parts.Select(part => part.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2 },
            Assert.ContainsSingle(page.Documents).Parts.Select(part => part.Order).ToArray());
    }

    [TestMethod]
    public async Task Two_documents_with_the_same_title_both_survive()
    {
        await store.AddAsync(Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);
        await store.AddAsync(Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);

        var page = await store.ListAsync("Invoices", StoredRange.All, 0, 10, CancellationToken.None);

        Assert.AreEqual(2, page.Total);
        Assert.AreEqual(2, page.Documents.Select(document => document.Id).Distinct().Count());
    }

    [TestMethod]
    public async Task Documents_come_back_oldest_first_across_day_boundaries()
    {
        foreach (var (day, title) in new[] { (3, "march"), (1, "january"), (2, "february") })
        {
            clock.Now = new DateTimeOffset(2026, day, 1, 0, 0, 0, TimeSpan.Zero);
            await store.AddAsync(
                new Document
                {
                    CabinetName = "Invoices",
                    Title = title,
                    IndexValues = Values(),
                    Parts = [],
                },
                CancellationToken.None);
        }

        var page = await store.ListAsync("Invoices", StoredRange.All, 0, 10, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "january", "february", "march" },
            page.Documents.Select(document => document.Title).ToArray());
    }

    [TestMethod]
    public async Task A_cabinet_with_nothing_in_it_is_an_empty_page()
    {
        var page = await store.ListAsync("Contracts", StoredRange.All, 0, 10, CancellationToken.None);

        Assert.IsEmpty(page.Documents);
        Assert.AreEqual(0, page.Total);
    }

    [TestMethod]
    public async Task Documents_of_one_cabinet_are_not_read_from_another()
    {
        await store.AddAsync(
            new Document
            {
                CabinetName = "Invoices",
                Title = "invoice",
                IndexValues = Values(),
                Parts = [],
            },
            CancellationToken.None);
        await store.AddAsync(
            new Document
            {
                CabinetName = "Contracts",
                Title = "contract",
                IndexValues = Values(),
                Parts = [],
            },
            CancellationToken.None);

        Assert.AreEqual(
            "invoice",
            Assert.ContainsSingle(
                (await store.ListAsync("Invoices", StoredRange.All, 0, 10, CancellationToken.None)).Documents).Title);
        Assert.AreEqual(
            "contract",
            Assert.ContainsSingle(
                (await store.ListAsync("Contracts", StoredRange.All, 0, 10, CancellationToken.None)).Documents).Title);
    }

    [TestMethod]
    public async Task A_failed_store_leaves_nothing_staged()
    {

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => store.AddAsync(Draft(new Part { Name = "page1.txt" }), CancellationToken.None));

        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "tmp")));
    }

    [TestMethod]
    public async Task A_stored_document_is_found_by_its_id()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        var found = await store.FindAsync("Invoices", stored.Id, CancellationToken.None);

        Assert.AreEqual(stored.Id, found.Id);
        Assert.AreEqual("invoice-001.txt", found.Title);
    }

    [TestMethod]
    public async Task An_id_the_cabinet_does_not_hold_is_refused()
    {
        await Add("invoice-001.txt", "page1.txt");
        var missing = Guid.CreateVersion7();

        var refusal = await Assert.ThrowsExactlyAsync<DocumentNotFoundException>(
            () => store.FindAsync("Invoices", missing, CancellationToken.None));

        Assert.AreEqual(missing, refusal.DocumentId);
    }

    [TestMethod]
    public async Task A_document_is_not_reachable_through_another_cabinet()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await Assert.ThrowsExactlyAsync<DocumentNotFoundException>(
            () => store.FindAsync("Contracts", stored.Id, CancellationToken.None));
    }

    [TestMethod]
    public async Task A_parts_content_is_read_back_byte_for_byte()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await using var content = await store.OpenPartAsync(
            "Invoices", stored.Id, stored.Parts[0].Id, CancellationToken.None);

        Assert.AreEqual("content of page1.txt", await new StreamReader(content).ReadToEndAsync());
    }

    [TestMethod]
    public async Task The_right_part_of_a_many_part_document_is_read()
    {
        var stored = await Add("delivery", "page1.txt", "page2.txt");

        await using var content = await store.OpenPartAsync(
            "Invoices", stored.Id, stored.Parts[1].Id, CancellationToken.None);

        Assert.AreEqual("content of page2.txt", await new StreamReader(content).ReadToEndAsync());
    }

    [TestMethod]
    public async Task A_part_id_the_document_does_not_have_is_refused()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await Assert.ThrowsExactlyAsync<DocumentNotFoundException>(
            () => store.OpenPartAsync(
                "Invoices", stored.Id, Guid.CreateVersion7(), CancellationToken.None));
    }

    [TestMethod]
    public async Task A_part_of_a_document_that_is_not_there_is_refused()
    {
        await Assert.ThrowsExactlyAsync<DocumentNotFoundException>(
            () => store.OpenPartAsync(
                "Invoices", Guid.CreateVersion7(), Guid.CreateVersion7(), CancellationToken.None));
    }

    [TestMethod]
    public async Task An_index_value_is_set_and_read_back()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await store.SetIndexValuesAsync(
            "Invoices",
            stored.Id,
            new Dictionary<string, IndexValue> { ["Batch"] = new IndexValue.Text("Exported on 2026-03-01") },
            CancellationToken.None);

        var reread = await store.FindAsync("Invoices", stored.Id, CancellationToken.None);
        Assert.IsInstanceOfType<IndexValue.Text>(reread.IndexValues["Batch"], out var batch);
        Assert.AreEqual("Exported on 2026-03-01", batch.Value);
    }

    [TestMethod]
    public async Task Setting_one_value_leaves_the_others_alone()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await store.SetIndexValuesAsync(
            "Invoices",
            stored.Id,
            new Dictionary<string, IndexValue> { ["Status"] = new IndexValue.Text("Done") },
            CancellationToken.None);

        var reread = await store.FindAsync("Invoices", stored.Id, CancellationToken.None);
        Assert.IsInstanceOfType<IndexValue.Text>(reread.IndexValues["Batch"], out var batch);
        Assert.IsInstanceOfType<IndexValue.Text>(reread.IndexValues["Status"], out var status);
        Assert.AreEqual("sample-material", batch.Value);
        Assert.AreEqual("Done", status.Value);
    }

    [TestMethod]
    public async Task A_value_is_matched_to_its_field_whatever_case_it_is_given_in()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await store.SetIndexValuesAsync(
            "Invoices",
            stored.Id,
            new Dictionary<string, IndexValue> { ["batch"] = new IndexValue.Text("again") },
            CancellationToken.None);

        var reread = await store.FindAsync("Invoices", stored.Id, CancellationToken.None);
        Assert.IsInstanceOfType<IndexValue.Text>(reread.IndexValues["Batch"], out var batch);
        Assert.AreEqual("again", batch.Value);
        Assert.ContainsSingle(reread.IndexValues);
    }

    [TestMethod]
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
        Assert.AreEqual(stored.StoredAt, reread.StoredAt);
        Assert.AreEqual(clock.Now, reread.ModifiedAt);
    }

    [TestMethod]
    public async Task A_change_leaves_the_parts_and_their_content_untouched()
    {
        var stored = await Add("delivery", "page1.txt", "page2.txt");

        await store.SetIndexValuesAsync(
            "Invoices",
            stored.Id,
            new Dictionary<string, IndexValue> { ["Batch"] = new IndexValue.Text("later") },
            CancellationToken.None);

        var reread = await store.FindAsync("Invoices", stored.Id, CancellationToken.None);
        CollectionAssert.AreEqual(
            new[] { "page1.txt", "page2.txt" },
            reread.Parts.Select(part => part.Name).ToArray());

        await using var content = await store.OpenPartAsync(
            "Invoices", stored.Id, stored.Parts[0].Id, CancellationToken.None);
        Assert.AreEqual("content of page1.txt", await new StreamReader(content).ReadToEndAsync());
    }

    [TestMethod]
    public async Task A_change_leaves_nothing_staged()
    {
        var stored = await Add("invoice-001.txt", "page1.txt");

        await store.SetIndexValuesAsync(
            "Invoices",
            stored.Id,
            new Dictionary<string, IndexValue> { ["Batch"] = new IndexValue.Text("later") },
            CancellationToken.None);

        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "tmp")));
    }

    [TestMethod]
    public async Task Changing_a_document_that_is_not_there_is_refused_and_changes_nothing()
    {
        var untouched = await Add("invoice-001.txt", "page1.txt");

        await Assert.ThrowsExactlyAsync<DocumentNotFoundException>(() => store.SetIndexValuesAsync(
            "Invoices",
            Guid.CreateVersion7(),
            new Dictionary<string, IndexValue> { ["Batch"] = new IndexValue.Text("later") },
            CancellationToken.None));

        var reread = await store.FindAsync("Invoices", untouched.Id, CancellationToken.None);
        Assert.IsInstanceOfType<IndexValue.Text>(reread.IndexValues["Batch"], out var batch);
        Assert.AreEqual("sample-material", batch.Value);
        Assert.IsEmpty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "tmp")));
    }

    [TestMethod]
    public async Task A_page_holds_only_the_documents_stored_inside_the_window()
    {
        await AddOn(new DateOnly(2026, 2, 27), "february.txt");
        await AddOn(new DateOnly(2026, 3, 1), "march-first.txt");
        await AddOn(new DateOnly(2026, 3, 2), "march-second.txt");

        var march = CalendarRange.Of(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1));

        var page = await store.ListAsync("Invoices", march, 0, 10, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "march-first.txt" },
            page.Documents.Select(document => document.Title).ToArray());
        Assert.AreEqual(1, page.Total);
    }

    [TestMethod]
    public async Task The_total_counts_only_what_the_window_holds()
    {
        await AddOn(new DateOnly(2026, 3, 1), "a.txt");
        await AddOn(new DateOnly(2026, 3, 1), "b.txt");
        await AddOn(new DateOnly(2026, 4, 1), "c.txt");

        var march = CalendarRange.Of(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var page = await store.ListAsync("Invoices", march, 0, 1, CancellationToken.None);

        Assert.AreEqual(2, page.Total);
        Assert.ContainsSingle(page.Documents);
    }

    [TestMethod]
    public async Task A_window_that_matches_nothing_is_an_empty_page()
    {
        await AddOn(new DateOnly(2026, 3, 1), "a.txt");

        var page = await store.ListAsync(
            "Invoices",
            CalendarRange.Of(new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)),
            0,
            10,
            CancellationToken.None);

        Assert.IsEmpty(page.Documents);
        Assert.AreEqual(0, page.Total);
    }

    [TestMethod]
    public async Task A_document_belongs_to_the_UTC_day_it_was_stored_on()
    {
        clock.Now = new DateTimeOffset(2026, 2, 28, 23, 0, 0, TimeSpan.Zero);
        await Add("late-february.txt", "page1.txt");

        var firstOfMarch = CalendarRange.Of(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1));
        var lastOfFebruary = CalendarRange.Of(new DateOnly(2026, 2, 28), new DateOnly(2026, 2, 28));

        Assert.IsEmpty(
            (await store.ListAsync("Invoices", firstOfMarch, 0, 10, CancellationToken.None)).Documents);
        Assert.ContainsSingle(
            (await store.ListAsync("Invoices", lastOfFebruary, 0, 10, CancellationToken.None)).Documents);
    }

    [TestMethod]
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

        CollectionAssert.AreEqual(
            new[] { "afternoon.txt" },
            page.Documents.Select(document => document.Title).ToArray());
        Assert.AreEqual(1, page.Total);
    }

    [TestCleanup]
    public void AfterEach()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static CancellationToken TestToken => CancellationToken.None;

    private static Func<Stream> Content =>
        () => new MemoryStream(Encoding.UTF8.GetBytes("page one"));

    private static IReadOnlyDictionary<string, IndexValue> Values() =>
        new Dictionary<string, IndexValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["Batch"] = new IndexValue.Text("sample-material"),
        };

    private static Document Draft(Part part) =>
        new()
        {
            CabinetName = "Invoices",
            Title = "invoice-001.txt",
            IndexValues = Values(),
            Parts = [part],
        };

    /// <summary>Stores a document whose parts each hold their own name as content.</summary>
    private Task<Document> Add(string title, params string[] partNames) =>
        store.AddAsync(
            new Document
            {
                CabinetName = "Invoices",
                Title = title,
                IndexValues = Values(),
                Parts =
                [
                    .. partNames.Select(name => Part.ToStore(
                        name,
                        () => new MemoryStream(Encoding.UTF8.GetBytes($"content of {name}")))),
                ],
            },
            CancellationToken.None);

    /// <summary>Stores a document as of midday on the given day.</summary>
    private Task<Document> AddOn(DateOnly day, string title)
    {
        clock.Now = new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

        return Add(title, "page1.txt");
    }

    /// <summary>A clock the test moves on purpose, so ordering and paging are not timing-dependent.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
