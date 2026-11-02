using System.Text;
using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Domain.Uploads;
using Nemetschek.Archive.Core.Services.Uploads;
using Nemetschek.Archive.Core.Tests.Utils;

namespace Nemetschek.Archive.Core.Tests.Services.Uploads;

/// <summary>
/// The folder-to-documents mapping: one test per rule in the brief's "Upload rules".
/// </summary>
[TestClass]
public sealed class UploadPlanningServiceTests
{
    private const string FolderName = "sample-material";

    private static readonly Cabinet Invoices = TestCabinets.Invoices();

    private static readonly IndexField Batch =
        Invoices.Fields.Single(field => field.Name == "Batch");

    private UploadPlanningService planning = null!;

    [TestInitialize]
    public void BeforeEach() => planning = new UploadPlanningService();

    [TestMethod]
    public void A_top_level_file_becomes_one_document_with_one_part()
    {
        var entry = Assert.ContainsSingle(Plan(files: ["invoice-001.txt"]));

        Assert.AreEqual("invoice-001.txt", entry.Entry);
        Assert.AreEqual(UploadEntryOutcome.Stored, entry.Outcome);
        CollectionAssert.AreEqual(new[] { "invoice-001.txt" }, PartNames(entry).ToArray());
    }

    [TestMethod]
    public void A_top_level_folder_becomes_one_document_whose_parts_are_its_files()
    {
        var entry = Assert.ContainsSingle(Plan(files:
            ["delivery-2026-03/page1.txt", "delivery-2026-03/page2.txt"]));

        Assert.AreEqual("delivery-2026-03", entry.Entry);
        CollectionAssert.AreEqual(new[] { "page1.txt", "page2.txt" }, PartNames(entry).ToArray());
    }

    [TestMethod]
    public void Each_top_level_entry_becomes_its_own_document()
    {
        var planned = Plan(files: ["a.txt", "b.txt", "folder/c.txt"]);

        CollectionAssert.AreEqual(new[] { "a.txt", "b.txt", "folder" }, Titles(planned).ToArray());
    }

    [TestMethod]
    public void An_empty_folder_the_client_names_becomes_a_document_with_no_parts()
    {
        var entry = Assert.ContainsSingle(Plan(folders: ["empty-folder"]));

        Assert.AreEqual("empty-folder", entry.Entry);
        Assert.AreEqual(UploadEntryOutcome.Stored, entry.Outcome);
        Assert.IsEmpty(PartNames(entry));
    }

    [TestMethod]
    public void A_folder_that_holds_files_is_not_duplicated_when_the_client_also_names_it()
    {
        var planned = Plan(files: ["delivery/page1.txt"], folders: ["delivery"]);

        var entry = Assert.ContainsSingle(planned);
        Assert.AreEqual("delivery", entry.Entry);
        CollectionAssert.AreEqual(new[] { "page1.txt" }, PartNames(entry).ToArray());
    }

    [TestMethod]
    public void A_file_nested_more_than_one_folder_deep_is_skipped_and_reported()
    {
        var planned = Plan(files: ["delivery/archive/old-page.txt"]);

        var skipped = Single(planned, UploadEntryOutcome.Skipped);
        Assert.AreEqual("delivery/archive/old-page.txt", skipped.Entry);
        Assert.Contains("nested more than one folder deep", skipped.Reason!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_folder_holding_only_a_nested_subfolder_still_becomes_a_document()
    {
        var planned = Plan(files: ["delivery/archive/old-page.txt"]);

        var stored = Single(planned, UploadEntryOutcome.Stored);
        Assert.AreEqual("delivery", stored.Entry);
        Assert.IsEmpty(PartNames(stored));
    }

    [TestMethod]
    public void A_nested_folder_is_reported_as_skipped_only_when_the_client_names_it()
    {
        var withoutIt = Plan(files: ["delivery/archive/old-page.txt"]);
        var withIt = Plan(files: ["delivery/archive/old-page.txt"], folders: ["delivery/archive"]);

        CollectionAssert.AreEqual(
            new[] { "delivery/archive/old-page.txt" },
            SkippedEntries(withoutIt).ToArray());
        CollectionAssert.AreEqual(
            new[] { "delivery/archive", "delivery/archive/old-page.txt" },
            SkippedEntries(withIt).Order(StringComparer.Ordinal).ToArray());
    }

    [TestMethod]
    public void Parts_are_ordered_ordinally_so_page10_sorts_before_page2()
    {
        var entry = Assert.ContainsSingle(Plan(files:
            ["delivery/page2.txt", "delivery/page10.txt", "delivery/page1.txt"]));

        CollectionAssert.AreEqual(
            new[] { "page1.txt", "page10.txt", "page2.txt" },
            PartNames(entry).ToArray());
    }

    [TestMethod]
    public void Documents_are_ordered_ordinally_by_title()
    {
        var planned = Plan(files: ["b.txt", "A.txt", "a.txt"]);

        CollectionAssert.AreEqual(new[] { "A.txt", "a.txt", "b.txt" }, Titles(planned).ToArray());
    }

    [TestMethod]
    public void The_title_is_the_entry_name_including_its_extension()
    {
        var entry = Assert.ContainsSingle(Plan(files: ["invoice-001.txt"]));

        Assert.AreEqual("invoice-001.txt", entry.Draft!.Title);
    }

    [TestMethod]
    public void The_chosen_field_is_set_to_the_uploaded_folder_name_on_every_document()
    {
        var planned = Plan(files: ["a.txt", "folder/b.txt"], folders: ["empty"]);

        Assert.AreEqual(3, planned.Count);
        foreach (var entry in planned)
        {
            Assert.IsInstanceOfType<IndexValue.Text>(entry.Draft!.IndexValues["Batch"], out var value);
            Assert.AreEqual(FolderName, value.Value);
        }
    }

    [TestMethod]
    public void The_chosen_field_is_the_only_index_value_set()
    {
        var entry = Assert.ContainsSingle(Plan(files: ["a.txt"]));

        CollectionAssert.AreEqual(new[] { "Batch" }, entry.Draft!.IndexValues.Keys.ToArray());
    }

    [TestMethod]
    public void The_field_is_keyed_case_insensitively_as_the_cabinet_names_it()
    {
        var entry = Assert.ContainsSingle(Plan(files: ["a.txt"]));

        Assert.IsTrue(entry.Draft!.IndexValues.ContainsKey("batch"));
    }

    [TestMethod]
    public void Every_document_is_planned_into_the_requested_cabinet()
    {
        var entry = Assert.ContainsSingle(Plan(files: ["a.txt"]));

        Assert.AreEqual("Invoices", entry.Draft!.CabinetName);
    }

    [TestMethod]
    public void A_planned_document_carries_no_id_and_no_timestamps_yet()
    {
        var draft = Assert.ContainsSingle(Plan(files: ["a.txt"])).Draft!;

        Assert.AreEqual(Guid.Empty, draft.Id);
        Assert.AreEqual(default, draft.StoredAt);
        Assert.AreEqual(default, draft.ModifiedAt);
    }

    [TestMethod]
    public void A_name_holding_a_control_character_is_skipped_and_reported()
    {
        var planned = Plan(files: ["bad\u0001name.txt"]);

        Assert.AreEqual(UploadEntryOutcome.Skipped, Assert.ContainsSingle(planned).Outcome);
    }

    [TestMethod]
    public void A_refused_path_stores_nothing_from_that_entry()
    {
        var planned = Plan(files: ["../outside.txt"]);

        Assert.DoesNotContain(entry => entry.Outcome == UploadEntryOutcome.Stored, planned);
    }

    [TestMethod]
    public void A_backslash_separates_segments_just_as_a_slash_does()
    {
        var entry = Assert.ContainsSingle(Plan(files: ["delivery\\page1.txt"]));

        Assert.AreEqual("delivery", entry.Entry);
        CollectionAssert.AreEqual(new[] { "page1.txt" }, PartNames(entry).ToArray());
    }

    [TestMethod]
    public void An_empty_upload_plans_nothing()
    {
        Assert.IsEmpty(Plan());
    }

    private IReadOnlyList<UploadEntry> Plan(
        string[]? files = null,
        string[]? folders = null) =>
        planning.PlanEntries(
            Invoices,
            Batch,
            new UploadRequest(
                Invoices.Name,
                Batch.Name,
                FolderName,
                [.. (files ?? []).Select(path => new UploadedFile(path, Content))],
                folders ?? []));

    private static Func<Stream> Content =>
        () => new MemoryStream(Encoding.UTF8.GetBytes("content"));

    private static IEnumerable<string> PartNames(UploadEntry entry) =>
        (entry.Draft?.Parts ?? []).Select(part => part.Name);

    private static IEnumerable<string> Titles(IReadOnlyList<UploadEntry> planned) =>
        planned.Where(entry => entry.Outcome == UploadEntryOutcome.Stored).Select(entry => entry.Entry);

    private static IEnumerable<string> SkippedEntries(IReadOnlyList<UploadEntry> planned) =>
        planned.Where(entry => entry.Outcome == UploadEntryOutcome.Skipped).Select(entry => entry.Entry);

    private static UploadEntry Single(IReadOnlyList<UploadEntry> planned, UploadEntryOutcome outcome) =>
        Assert.ContainsSingle(entry => entry.Outcome == outcome, planned);
}
