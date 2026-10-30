using System.Text;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.Uploads;
using Nemetschek.Archive.Core.Exceptions.Cabinets;
using Nemetschek.Archive.Core.Exceptions.Uploads;
using Nemetschek.Archive.Core.Services.Cabinets;
using Nemetschek.Archive.Core.Services.Documents;
using Nemetschek.Archive.Core.Services.Interfaces.Documents;
using Nemetschek.Archive.Core.Services.Uploads;
using Nemetschek.Archive.Core.Tests.Cabinets;

namespace Nemetschek.Archive.Core.Tests.Uploads;

/// <summary>
/// Running an upload: what is refused outright, what becomes a row, and what the report says.
/// </summary>
// The store is a fake, so these are rule tests and not storage tests: the real store is covered by
// DocumentsStoreServiceTests against a temp directory. The split matters for the brief's "a partial
// failure is never silent" — the only way to assert it is to make one document fail on demand.
public sealed class UploadServiceTests
{
    // --- refusals, which are terminal and store nothing ----------------------------------------

    [Fact]
    public async Task An_unknown_cabinet_is_refused()
    {
        var store = new FakeStore();

        var refusal = await Assert.ThrowsAsync<UnknownCabinetException>(
            () => Upload(store, cabinetName: "Nonsense"));

        Assert.Equal("Nonsense", refusal.CabinetName);
        Assert.Empty(store.Added);
    }

    [Theory]
    [InlineData("ApprovedBy", "is read-only and cannot be chosen")]
    [InlineData("Amount", "holds a number value; a text field is required")]
    [InlineData("InvoiceDate", "holds a date value; a text field is required")]
    [InlineData("Nonsense", "has no index field 'Nonsense'")]
    public async Task A_field_that_may_not_be_chosen_is_refused(string fieldName, string reason)
    {
        var store = new FakeStore();

        var refusal = await Assert.ThrowsAsync<UploadRefusedException>(
            () => Upload(store, fieldName: fieldName));

        Assert.Contains(reason, refusal.Message, StringComparison.Ordinal);
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task A_folder_name_longer_than_the_field_allows_is_refused()
    {
        // Status accepts 12 characters; "sample-material" is 15.
        var store = new FakeStore();

        var refusal = await Assert.ThrowsAsync<UploadRefusedException>(
            () => Upload(store, fieldName: "Status"));

        Assert.Equal("Status", refusal.FieldName);
        Assert.Contains("at most 12 characters", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("the value has 15", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(store.Added);
    }

    [Theory]
    [InlineData("has/a/slash")]
    [InlineData("has\\a\\backslash")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("/rooted")]
    [InlineData("  ")]
    public async Task A_folder_name_that_is_not_a_single_usable_name_is_refused(string folderName)
    {
        var store = new FakeStore();

        var refusal = await Assert.ThrowsAsync<UploadRefusedException>(
            () => Upload(store, folderName: folderName));

        Assert.Contains("is not a usable folder name", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(store.Added);
    }

    [Fact]
    public async Task The_chosen_field_is_matched_case_insensitively_and_reported_canonically()
    {
        var report = await Upload(new FakeStore(), fieldName: "bAtCh");

        Assert.Equal("Batch", report.FieldName);
    }

    // --- the report ---------------------------------------------------------------------------

    [Fact]
    public async Task The_report_names_the_cabinet_the_field_and_the_folder()
    {
        var report = await Upload(new FakeStore());

        Assert.Equal("Invoices", report.CabinetName);
        Assert.Equal("Batch", report.FieldName);
        Assert.Equal("sample-material", report.FolderName);
    }

    [Fact]
    public async Task A_stored_entry_carries_its_document_id_and_part_names()
    {
        var report = await Upload(new FakeStore(), files: ["delivery/page1.txt", "delivery/page2.txt"]);

        var entry = Assert.Single(report.Results);
        Assert.Equal(UploadEntryOutcome.Stored, entry.Outcome);
        Assert.NotNull(entry.DocumentId);
        Assert.NotEqual(Guid.Empty, entry.DocumentId!.Value);
        Assert.Equal(["page1.txt", "page2.txt"], entry.Parts);
        Assert.Null(entry.Reason);
    }

    [Fact]
    public async Task A_stored_entry_keeps_no_draft_once_it_is_stored()
    {
        // The draft holds open-content callbacks; leaving it on the report would keep them alive.
        var report = await Upload(new FakeStore(), files: ["a.txt"]);

        Assert.Null(Assert.Single(report.Results).Draft);
    }

    [Fact]
    public async Task Results_are_ordered_ordinally_by_entry_name()
    {
        var report = await Upload(
            new FakeStore(),
            files: ["b.txt", "A.txt", "../refused.txt", "a.txt"]);

        Assert.Equal(
            ["../refused.txt", "A.txt", "a.txt", "b.txt"],
            report.Results.Select(entry => entry.Entry));
    }

    [Fact]
    public async Task Every_entry_the_Archive_saw_gets_exactly_one_row()
    {
        var report = await Upload(
            new FakeStore(),
            files: ["a.txt", "folder/b.txt", "folder/deep/c.txt"],
            folders: ["empty"]);

        Assert.Equal(
            ["a.txt", "empty", "folder", "folder/deep/c.txt"],
            report.Results.Select(entry => entry.Entry));
        Assert.Equal(
            [UploadEntryOutcome.Stored, UploadEntryOutcome.Stored, UploadEntryOutcome.Stored,
                UploadEntryOutcome.Skipped],
            report.Results.Select(entry => entry.Outcome));
    }

    // --- a partial failure is never silent -----------------------------------------------------

    [Fact]
    public async Task One_document_failing_does_not_stop_the_others()
    {
        var store = new FakeStore { FailFor = "b.txt" };

        var report = await Upload(store, files: ["a.txt", "b.txt", "c.txt"]);

        Assert.Equal(
            [UploadEntryOutcome.Stored, UploadEntryOutcome.Failed, UploadEntryOutcome.Stored],
            report.Results.Select(entry => entry.Outcome));
        Assert.Equal(["a.txt", "c.txt"], store.Added.Select(document => document.Title));
    }

    [Fact]
    public async Task A_failed_entry_reports_the_reason_it_failed()
    {
        var store = new FakeStore { FailFor = "a.txt" };

        var report = await Upload(store, files: ["a.txt"]);

        var entry = Assert.Single(report.Results);
        Assert.Equal(UploadEntryOutcome.Failed, entry.Outcome);
        Assert.Equal("the volume is full", entry.Reason);
        Assert.Null(entry.DocumentId);
    }

    [Fact]
    public async Task Cancellation_is_not_turned_into_a_failed_row()
    {
        // There is nobody left to read the report, so cancellation propagates instead.
        var store = new FakeStore { CancelFor = "a.txt" };

        await Assert.ThrowsAsync<OperationCanceledException>(() => Upload(store, files: ["a.txt"]));
    }

    [Fact]
    public async Task A_skipped_entry_never_reaches_the_store()
    {
        var store = new FakeStore();

        await Upload(store, files: ["../outside.txt", "a.txt"]);

        Assert.Equal(["a.txt"], store.Added.Select(document => document.Title));
    }

    [Fact]
    public async Task Each_stored_document_carries_the_folder_name_in_the_chosen_field()
    {
        var store = new FakeStore();

        await Upload(store, files: ["a.txt", "b.txt"]);

        Assert.Equal(2, store.Added.Count);
        foreach (var document in store.Added)
        {
            Assert.Equal(
                "sample-material",
                Assert.IsType<Core.Domain.IndexFields.IndexValue.Text>(
                    document.IndexValues["Batch"]).Value);
        }
    }

    // --- helpers ------------------------------------------------------------------------------

    private static Task<UploadReport> Upload(
        FakeStore store,
        string cabinetName = "Invoices",
        string fieldName = "Batch",
        string folderName = "sample-material",
        string[]? files = null,
        string[]? folders = null) =>
        new UploadService(
                new CabinetCatalog([TestCabinets.Invoices(), TestCabinets.Contracts()]),
                store,
                new UploadPlanningService())
            .UploadAsync(
                new UploadRequest(
                    cabinetName,
                    fieldName,
                    folderName,
                    [.. (files ?? ["a.txt"]).Select(path => new UploadedFile(path, Content))],
                    folders ?? []),
                CancellationToken.None);

    private static Func<Stream> Content =>
        () => new MemoryStream(Encoding.UTF8.GetBytes("content"));

    /// <summary>An <see cref="IDocumentStore"/> that records what it was given and can be made to fail.</summary>
    private sealed class FakeStore : IDocumentStore
    {
        public List<Document> Added { get; } = [];

        /// <summary>The title to raise a storage failure for.</summary>
        public string? FailFor { get; init; }

        /// <summary>The title to raise a cancellation for.</summary>
        public string? CancelFor { get; init; }

        public Task<Document> AddAsync(Document document, CancellationToken cancellationToken)
        {
            if (document.Title == CancelFor)
            {
                throw new OperationCanceledException();
            }

            if (document.Title == FailFor)
            {
                throw new IOException("the volume is full");
            }

            Added.Add(document);

            return Task.FromResult(document with
            {
                Id = Guid.CreateVersion7(),
                Parts = [.. document.Parts.Select(part => part with { OpenContent = null })],
            });
        }

        public Task<DocumentPage> ListAsync(
            string cabinetName,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentPage([], 0));
    }
}
