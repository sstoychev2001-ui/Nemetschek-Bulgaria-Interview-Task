using System.Text;
using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Domain.Uploads;
using Nemetschek.Archive.Core.Services.Uploads;
using Nemetschek.Archive.Core.Tests.Cabinets;

namespace Nemetschek.Archive.Core.Tests.Uploads;

/// <summary>
/// The folder-to-documents mapping: one test per rule in the brief's "Upload rules".
/// </summary>
public sealed class UploadPlanningServiceTests
{
    private const string FolderName = "sample-material";

    private static readonly Cabinet Invoices = TestCabinets.Invoices();

    private static readonly IndexField Batch =
        Invoices.Fields.Single(field => field.Name == "Batch");

    [Fact]
    public void A_top_level_file_becomes_one_document_with_one_part()
    {
        var entry = Assert.Single(Plan(files: ["invoice-001.txt"]));

        Assert.Equal("invoice-001.txt", entry.Entry);
        Assert.Equal(UploadEntryOutcome.Stored, entry.Outcome);
        Assert.Equal(["invoice-001.txt"], PartNames(entry));
    }

    [Fact]
    public void A_top_level_folder_becomes_one_document_whose_parts_are_its_files()
    {
        var entry = Assert.Single(Plan(files:
            ["delivery-2026-03/page1.txt", "delivery-2026-03/page2.txt"]));

        Assert.Equal("delivery-2026-03", entry.Entry);
        Assert.Equal(["page1.txt", "page2.txt"], PartNames(entry));
    }

    [Fact]
    public void Each_top_level_entry_becomes_its_own_document()
    {
        var planned = Plan(files: ["a.txt", "b.txt", "folder/c.txt"]);

        Assert.Equal(["a.txt", "b.txt", "folder"], Titles(planned));
    }

    [Fact]
    public void An_empty_folder_the_client_names_becomes_a_document_with_no_parts()
    {
        var entry = Assert.Single(Plan(folders: ["empty-folder"]));

        Assert.Equal("empty-folder", entry.Entry);
        Assert.Equal(UploadEntryOutcome.Stored, entry.Outcome);
        Assert.Empty(PartNames(entry));
    }

    [Fact]
    public void A_folder_that_holds_files_is_not_duplicated_when_the_client_also_names_it()
    {
        var planned = Plan(files: ["delivery/page1.txt"], folders: ["delivery"]);

        var entry = Assert.Single(planned);
        Assert.Equal("delivery", entry.Entry);
        Assert.Equal(["page1.txt"], PartNames(entry));
    }

    [Fact]
    public void A_file_nested_more_than_one_folder_deep_is_skipped_and_reported()
    {
        var planned = Plan(files: ["delivery/archive/old-page.txt"]);

        var skipped = Single(planned, UploadEntryOutcome.Skipped);
        Assert.Equal("delivery/archive/old-page.txt", skipped.Entry);
        Assert.Contains("nested more than one folder deep", skipped.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_folder_holding_only_a_nested_subfolder_still_becomes_a_document()
    {
        var planned = Plan(files: ["delivery/archive/old-page.txt"]);

        var stored = Single(planned, UploadEntryOutcome.Stored);
        Assert.Equal("delivery", stored.Entry);
        Assert.Empty(PartNames(stored));
    }

    [Fact]
    public void A_nested_folder_is_reported_as_skipped_only_when_the_client_names_it()
    {
        var withoutIt = Plan(files: ["delivery/archive/old-page.txt"]);
        var withIt = Plan(files: ["delivery/archive/old-page.txt"], folders: ["delivery/archive"]);

        Assert.Equal(["delivery/archive/old-page.txt"], SkippedEntries(withoutIt));
        Assert.Equal(
            ["delivery/archive", "delivery/archive/old-page.txt"],
            SkippedEntries(withIt).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Parts_are_ordered_ordinally_so_page10_sorts_before_page2()
    {
        var entry = Assert.Single(Plan(files:
            ["delivery/page2.txt", "delivery/page10.txt", "delivery/page1.txt"]));

        Assert.Equal(["page1.txt", "page10.txt", "page2.txt"], PartNames(entry));
    }

    [Fact]
    public void Documents_are_ordered_ordinally_by_title()
    {
        var planned = Plan(files: ["b.txt", "A.txt", "a.txt"]);

        Assert.Equal(["A.txt", "a.txt", "b.txt"], Titles(planned));
    }

    [Fact]
    public void The_title_is_the_entry_name_including_its_extension()
    {
        var entry = Assert.Single(Plan(files: ["invoice-001.txt"]));

        Assert.Equal("invoice-001.txt", entry.Draft!.Title);
    }

    [Fact]
    public void The_chosen_field_is_set_to_the_uploaded_folder_name_on_every_document()
    {
        var planned = Plan(files: ["a.txt", "folder/b.txt"], folders: ["empty"]);

        Assert.Equal(3, planned.Count);
        foreach (var entry in planned)
        {
            var value = Assert.IsType<IndexValue.Text>(entry.Draft!.IndexValues["Batch"]);
            Assert.Equal(FolderName, value.Value);
        }
    }

    [Fact]
    public void The_chosen_field_is_the_only_index_value_set()
    {
        var entry = Assert.Single(Plan(files: ["a.txt"]));

        Assert.Equal(["Batch"], entry.Draft!.IndexValues.Keys);
    }

    [Fact]
    public void The_field_is_keyed_case_insensitively_as_the_cabinet_names_it()
    {
        var entry = Assert.Single(Plan(files: ["a.txt"]));

        Assert.True(entry.Draft!.IndexValues.ContainsKey("batch"));
    }

    [Fact]
    public void Every_document_is_planned_into_the_requested_cabinet()
    {
        var entry = Assert.Single(Plan(files: ["a.txt"]));

        Assert.Equal("Invoices", entry.Draft!.CabinetName);
    }

    [Fact]
    public void A_planned_document_carries_no_id_and_no_timestamps_yet()
    {
        var draft = Assert.Single(Plan(files: ["a.txt"])).Draft!;

        Assert.Equal(Guid.Empty, draft.Id);
        Assert.Equal(default, draft.StoredAt);
        Assert.Equal(default, draft.ModifiedAt);
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("\\windows\\system32")]
    [InlineData("C:/secrets.txt")]
    [InlineData("../outside.txt")]
    [InlineData("folder/../../outside.txt")]
    [InlineData("./here.txt")]
    [InlineData("")]
    [InlineData("   ")]
    public void A_path_that_is_not_inside_the_uploaded_folder_is_skipped_and_reported(string path)
    {
        var skipped = Single(Plan(files: [path]), UploadEntryOutcome.Skipped);

        Assert.Equal(path, skipped.Entry);
        Assert.Contains("not a path inside the uploaded folder", skipped.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_name_holding_a_control_character_is_skipped_and_reported()
    {
        var planned = Plan(files: ["bad\u0001name.txt"]);

        Assert.Equal(UploadEntryOutcome.Skipped, Assert.Single(planned).Outcome);
    }

    [Fact]
    public void A_refused_path_stores_nothing_from_that_entry()
    {
        var planned = Plan(files: ["../outside.txt"]);

        Assert.DoesNotContain(planned, entry => entry.Outcome == UploadEntryOutcome.Stored);
    }

    [Fact]
    public void A_backslash_separates_segments_just_as_a_slash_does()
    {
        var entry = Assert.Single(Plan(files: ["delivery\\page1.txt"]));

        Assert.Equal("delivery", entry.Entry);
        Assert.Equal(["page1.txt"], PartNames(entry));
    }

    [Theory]
    [InlineData("Договор София.txt")]
    [InlineData("Фактури Q1")]
    [InlineData("readme with spaces.txt")]
    [InlineData("naïve-café.txt")]
    public void A_unicode_name_is_kept_exactly_as_the_user_had_it(string name)
    {
        var entry = Assert.Single(Plan(files: [name]));

        Assert.Equal(name, entry.Entry);
        Assert.Equal(name, entry.Draft!.Title);
    }

    [Fact]
    public void An_empty_upload_plans_nothing()
    {
        Assert.Empty(Plan());
    }

    private static IReadOnlyList<UploadEntry> Plan(
        string[]? files = null,
        string[]? folders = null) =>
        new UploadPlanningService().PlanEntries(
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
        Assert.Single(planned, entry => entry.Outcome == outcome);
}
