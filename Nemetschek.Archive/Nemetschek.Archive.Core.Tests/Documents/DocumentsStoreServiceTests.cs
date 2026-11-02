using System.Text;
using Microsoft.Extensions.Logging;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Services.Documents;
using Nemetschek.Archive.Core.Utils.Documents;

namespace Nemetschek.Archive.Core.Tests.Documents;

/// <summary>
/// What the store does and what it writes down. Runs against a throwaway directory: no container,
/// no network.
/// </summary>
public sealed class DocumentsStoreServiceTests : IDisposable
{
    private readonly string root =
        Path.Combine(Path.GetTempPath(), $"archive-tests-{Guid.NewGuid():N}");

    private readonly RecordingLogger<DocumentsStoreService> logger = new();

    private readonly FixedClock clock = new(new DateTimeOffset(2026, 3, 1, 9, 30, 0, TimeSpan.Zero));

    /// <summary>A store on a prepared, empty storage root.</summary>
    private DocumentsStoreService Store()
    {
        Directory.CreateDirectory(DocumentStorageUtils.Staging(root));

        return new DocumentsStoreService(root, clock, logger);
    }

    [Fact]
    public async Task A_stored_document_is_logged_with_its_id()
    {
        var stored = await Store().AddAsync(
            Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains(stored.Id.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Contains("Invoices", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_store_failure_is_logged_with_its_exception_and_rethrown()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Store().AddAsync(Draft(new Part { Name = "page1.txt" }), CancellationToken.None));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(failure, entry.Exception);
    }

    [Fact]
    public async Task A_cancelled_store_is_logged_at_Error_like_any_other_failure()
    {
        var draft = Draft(Part.ToStore("page1.txt", () => throw new OperationCanceledException()));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Store().AddAsync(draft, CancellationToken.None));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<OperationCanceledException>(entry.Exception);
    }

    [Fact]
    public async Task Reading_a_page_logs_nothing()
    {
        await Store().ListAsync("Invoices", StoredRange.All, skip: 0, take: 10, CancellationToken.None);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task A_stored_document_comes_back_from_disk_as_it_went_in()
    {
        var store = Store();
        await store.AddAsync(Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);

        var page = await store.ListAsync("Invoices", StoredRange.All, 0, 10, CancellationToken.None);

        var document = Assert.Single(page.Documents);
        Assert.Equal("invoice-001.txt", document.Title);
        Assert.Equal("Invoices", document.CabinetName);
        Assert.Equal(
            "sample-material",
            Assert.IsType<IndexValue.Text>(document.IndexValues["Batch"]).Value);
        var part = Assert.Single(document.Parts);
        Assert.Equal("page1.txt", part.Name);
        Assert.Equal(0, part.Order);
        Assert.Equal(8, part.SizeInBytes);
    }

    [Fact]
    public async Task Part_content_is_stored_byte_for_byte()
    {
        await Store().AddAsync(Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);

        var file = Assert.Single(
            Directory.EnumerateFiles(root, "*.bin", SearchOption.AllDirectories));
        Assert.Equal("page one", await File.ReadAllTextAsync(file, TestToken));
    }

    [Fact]
    public async Task The_store_sets_the_system_fields_and_ignores_what_arrived()
    {
        var arrived = Draft(Part.ToStore("page1.txt", Content)) with
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            StoredAt = DateTimeOffset.UnixEpoch,
            ModifiedAt = DateTimeOffset.UnixEpoch,
        };

        var stored = await Store().AddAsync(arrived, CancellationToken.None);

        Assert.NotEqual(arrived.Id, stored.Id);
        Assert.Equal(clock.GetUtcNow(), stored.StoredAt);
        Assert.Equal(clock.GetUtcNow(), stored.ModifiedAt);
    }

    [Fact]
    public async Task An_id_carries_the_time_it_was_stored()
    {
        var stored = await Store().AddAsync(
            Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);

        Assert.Equal(7, stored.Id.Version);
    }

    [Fact]
    public async Task The_content_callback_is_cleared_once_the_part_is_stored()
    {
        var stored = await Store().AddAsync(
            Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);

        Assert.Null(Assert.Single(stored.Parts).OpenContent);
    }

    [Fact]
    public async Task A_users_name_never_becomes_a_file_name()
    {
        var store = Store();
        var stored = await store.AddAsync(
            Draft(Part.ToStore("Договор София.txt", Content)), CancellationToken.None);

        var partFile = Assert.Single(
            Directory.EnumerateFiles(root, "*.bin", SearchOption.AllDirectories));
        Assert.Contains(
            Assert.Single(stored.Parts).Id.ToString(),
            Path.GetFileName(partFile),
            StringComparison.Ordinal);
        Assert.DoesNotContain("Договор", partFile, StringComparison.Ordinal);

        var page = await store.ListAsync("Invoices", StoredRange.All, 0, 10, CancellationToken.None);
        Assert.Equal("Договор София.txt", Assert.Single(page.Documents).Parts[0].Name);
    }

    [Fact]
    public async Task A_document_with_no_parts_is_stored()
    {
        var store = Store();
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

        Assert.Empty(Assert.Single(page.Documents).Parts);
    }

    [Fact]
    public async Task A_zero_byte_part_is_stored_with_a_size_of_zero()
    {
        var stored = await Store().AddAsync(
            Draft(Part.ToStore("empty-note.txt", () => new MemoryStream())),
            CancellationToken.None);

        Assert.Equal(0, Assert.Single(stored.Parts).SizeInBytes);
    }

    [Fact]
    public async Task Parts_keep_the_order_they_were_given()
    {
        var store = Store();
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

        Assert.Equal(
            ["page1.txt", "page10.txt", "page2.txt"],
            Assert.Single(page.Documents).Parts.Select(part => part.Name));
        Assert.Equal([0, 1, 2], Assert.Single(page.Documents).Parts.Select(part => part.Order));
    }

    [Fact]
    public async Task Two_documents_with_the_same_title_both_survive()
    {
        var store = Store();
        await store.AddAsync(Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);
        await store.AddAsync(Draft(Part.ToStore("page1.txt", Content)), CancellationToken.None);

        var page = await store.ListAsync("Invoices", StoredRange.All, 0, 10, CancellationToken.None);

        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Documents.Select(document => document.Id).Distinct().Count());
    }

    [Fact]
    public async Task Documents_come_back_oldest_first_across_day_boundaries()
    {
        var store = Store();
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

        Assert.Equal(
            ["january", "february", "march"],
            page.Documents.Select(document => document.Title));
    }

    [Theory]
    [InlineData(0, 2, new[] { "a", "b" })]
    [InlineData(2, 2, new[] { "c", "d" })]
    [InlineData(4, 2, new[] { "e" })]
    [InlineData(5, 2, new string[0])]
    public async Task A_page_holds_the_documents_it_was_asked_for(
        int skip,
        int take,
        string[] expected)
    {
        var store = Store();
        foreach (var title in new[] { "a", "b", "c", "d", "e" })
        {
            clock.Now = clock.Now.AddMinutes(1);
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

        var page = await store.ListAsync("Invoices", StoredRange.All, skip, take, CancellationToken.None);

        Assert.Equal(expected, page.Documents.Select(document => document.Title));
        Assert.Equal(5, page.Total);
    }

    [Fact]
    public async Task A_cabinet_with_nothing_in_it_is_an_empty_page()
    {
        var page = await Store().ListAsync("Contracts", StoredRange.All, 0, 10, CancellationToken.None);

        Assert.Empty(page.Documents);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task Documents_of_one_cabinet_are_not_read_from_another()
    {
        var store = Store();
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

        Assert.Equal(
            "invoice",
            Assert.Single((await store.ListAsync("Invoices", StoredRange.All, 0, 10, CancellationToken.None)).Documents).Title);
        Assert.Equal(
            "contract",
            Assert.Single((await store.ListAsync("Contracts", StoredRange.All, 0, 10, CancellationToken.None)).Documents).Title);
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("nested/name")]
    [InlineData("nested\\name")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("")]
    public async Task A_cabinet_name_that_is_not_a_single_directory_name_is_refused(string cabinetName)
    {
        var store = Store();

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.ListAsync(cabinetName, StoredRange.All, 0, 10, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(
            () => store.AddAsync(
                new Document
                {
                    CabinetName = cabinetName,
                    Title = "title",
                    IndexValues = Values(),
                    Parts = [],
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task A_failed_store_leaves_nothing_staged()
    {
        var store = Store();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.AddAsync(Draft(new Part { Name = "page1.txt" }), CancellationToken.None));

        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "tmp")));
    }

    public void Dispose()
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

    /// <summary>A clock the test moves on purpose, so ordering and paging are not timing-dependent.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>An <see cref="ILogger{T}"/> that keeps every entry written to it.</summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
