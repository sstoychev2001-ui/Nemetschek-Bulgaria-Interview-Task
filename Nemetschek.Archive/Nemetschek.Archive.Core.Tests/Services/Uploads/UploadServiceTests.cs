using System.Text;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Domain.Uploads;
using Nemetschek.Archive.Core.Exceptions.Cabinets;
using Nemetschek.Archive.Core.Exceptions.Uploads;
using Nemetschek.Archive.Core.Services.Cabinets;
using Nemetschek.Archive.Core.Services.Documents;
using Nemetschek.Archive.Core.Services.Interfaces.Documents;
using Nemetschek.Archive.Core.Services.Uploads;
using Nemetschek.Archive.Core.Tests.Utils;

namespace Nemetschek.Archive.Core.Tests.Services.Uploads;

/// <summary>
/// Running an upload: what is refused outright, what becomes a row, and what the report says.
/// </summary>
[TestClass]
public sealed class UploadServiceTests
{
    [TestMethod]
    public async Task An_unknown_cabinet_is_refused()
    {
        var store = new FakeStore();

        var refusal = await Assert.ThrowsExactlyAsync<UnknownCabinetException>(
            () => Upload(store, cabinetName: "Nonsense"));

        Assert.AreEqual("Nonsense", refusal.CabinetName);
        Assert.IsEmpty(store.Added);
    }

    [TestMethod]
    public async Task A_folder_name_longer_than_the_field_allows_is_refused()
    {
        var store = new FakeStore();

        var refusal = await Assert.ThrowsExactlyAsync<UploadRefusedException>(
            () => Upload(store, fieldName: "Status"));

        Assert.AreEqual("Status", refusal.FieldName);
        Assert.Contains("at most 12 characters", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("the value has 15", refusal.Message, StringComparison.Ordinal);
        Assert.IsEmpty(store.Added);
    }

    [TestMethod]
    public async Task The_chosen_field_is_matched_case_insensitively_and_reported_canonically()
    {
        var report = await Upload(new FakeStore(), fieldName: "bAtCh");

        Assert.AreEqual("Batch", report.FieldName);
    }

    [TestMethod]
    public async Task The_report_names_the_cabinet_the_field_and_the_folder()
    {
        var report = await Upload(new FakeStore());

        Assert.AreEqual("Invoices", report.CabinetName);
        Assert.AreEqual("Batch", report.FieldName);
        Assert.AreEqual("sample-material", report.FolderName);
    }

    [TestMethod]
    public async Task A_stored_entry_carries_its_document_id_and_part_names()
    {
        var report = await Upload(new FakeStore(), files: ["delivery/page1.txt", "delivery/page2.txt"]);

        var entry = Assert.ContainsSingle(report.Results);
        Assert.AreEqual(UploadEntryOutcome.Stored, entry.Outcome);
        Assert.IsNotNull(entry.DocumentId);
        Assert.AreNotEqual(Guid.Empty, entry.DocumentId!.Value);
        CollectionAssert.AreEqual(new[] { "page1.txt", "page2.txt" }, entry.Parts.ToArray());
        Assert.IsNull(entry.Reason);
    }

    [TestMethod]
    public async Task A_stored_entry_keeps_no_draft_once_it_is_stored()
    {
        var report = await Upload(new FakeStore(), files: ["a.txt"]);

        Assert.IsNull(Assert.ContainsSingle(report.Results).Draft);
    }

    [TestMethod]
    public async Task Results_are_ordered_ordinally_by_entry_name()
    {
        var report = await Upload(
            new FakeStore(),
            files: ["b.txt", "A.txt", "../refused.txt", "a.txt"]);

        CollectionAssert.AreEqual(
            new[] { "../refused.txt", "A.txt", "a.txt", "b.txt" },
            report.Results.Select(entry => entry.Entry).ToArray());
    }

    [TestMethod]
    public async Task Every_entry_the_Archive_saw_gets_exactly_one_row()
    {
        var report = await Upload(
            new FakeStore(),
            files: ["a.txt", "folder/b.txt", "folder/deep/c.txt"],
            folders: ["empty"]);

        CollectionAssert.AreEqual(
            new[] { "a.txt", "empty", "folder", "folder/deep/c.txt" },
            report.Results.Select(entry => entry.Entry).ToArray());
        CollectionAssert.AreEqual(
            new[] { UploadEntryOutcome.Stored, UploadEntryOutcome.Stored, UploadEntryOutcome.Stored,
                UploadEntryOutcome.Skipped },
            report.Results.Select(entry => entry.Outcome).ToArray());
    }

    [TestMethod]
    public async Task One_document_failing_does_not_stop_the_others()
    {
        var store = new FakeStore { FailFor = "b.txt" };

        var report = await Upload(store, files: ["a.txt", "b.txt", "c.txt"]);

        CollectionAssert.AreEqual(
            new[] { UploadEntryOutcome.Stored, UploadEntryOutcome.Failed, UploadEntryOutcome.Stored },
            report.Results.Select(entry => entry.Outcome).ToArray());
        CollectionAssert.AreEqual(
            new[] { "a.txt", "c.txt" },
            store.Added.Select(document => document.Title).ToArray());
    }

    [TestMethod]
    public async Task A_failed_entry_reports_the_reason_it_failed()
    {
        var store = new FakeStore { FailFor = "a.txt" };

        var report = await Upload(store, files: ["a.txt"]);

        var entry = Assert.ContainsSingle(report.Results);
        Assert.AreEqual(UploadEntryOutcome.Failed, entry.Outcome);
        Assert.AreEqual("the volume is full", entry.Reason);
        Assert.IsNull(entry.DocumentId);
    }

    [TestMethod]
    public async Task Cancellation_is_not_turned_into_a_failed_row()
    {
        var store = new FakeStore { CancelFor = "a.txt" };

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => Upload(store, files: ["a.txt"]));
    }

    [TestMethod]
    public async Task A_skipped_entry_never_reaches_the_store()
    {
        var store = new FakeStore();

        await Upload(store, files: ["../outside.txt", "a.txt"]);

        CollectionAssert.AreEqual(
            new[] { "a.txt" },
            store.Added.Select(document => document.Title).ToArray());
    }

    [TestMethod]
    public async Task Each_stored_document_carries_the_folder_name_in_the_chosen_field()
    {
        var store = new FakeStore();

        await Upload(store, files: ["a.txt", "b.txt"]);

        Assert.AreEqual(2, store.Added.Count);
        foreach (var document in store.Added)
        {
            Assert.IsInstanceOfType<IndexValue.Text>(document.IndexValues["Batch"], out var batch);
            Assert.AreEqual("sample-material", batch.Value);
        }
    }

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
            StoredRange storedAt,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentPage([], 0));

        public Task<Document> FindAsync(
            string cabinetName,
            Guid documentId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("An upload does not read documents back.");

        public Task<Stream> OpenPartAsync(
            string cabinetName,
            Guid documentId,
            Guid partId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("An upload does not read part content.");

        public Task SetIndexValuesAsync(
            string cabinetName,
            Guid documentId,
            IReadOnlyDictionary<string, IndexValue> values,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("An upload sets index values as it stores.");
    }
}
