using Nemetschek.Archive.Core.Utils.Documents;

namespace Nemetschek.Archive.Core.Tests.Utils.Documents;

/// <summary>
/// The storage layout's names and the two parsers that read them back. No filesystem involved.
/// </summary>
[TestClass]
public sealed class DocumentStorageUtilsTests
{
    private const string Root = "/srv/archive";

    [TestMethod]
    public void Staging_sits_directly_under_the_root()
    {
        Assert.AreEqual(Path.Combine(Root, "tmp"), DocumentStorageUtils.Staging(Root));
    }

    [TestMethod]
    public void A_staging_path_is_unique_and_inside_staging()
    {
        var first = DocumentStorageUtils.StagingPath(Root);
        var second = DocumentStorageUtils.StagingPath(Root);

        Assert.AreNotEqual(first, second);
        Assert.StartsWith(DocumentStorageUtils.Staging(Root), first, StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_staging_path_takes_the_extension_it_is_given()
    {
        Assert.EndsWith(".json", DocumentStorageUtils.StagingPath(Root, ".json"), StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_cabinet_sits_under_the_cabinets_directory()
    {
        Assert.AreEqual(
            Path.Combine(Root, "cabinets", "Invoices"),
            DocumentStorageUtils.CabinetDirectory(Root, "Invoices"));
    }

    [TestMethod]
    public void A_unicode_cabinet_name_is_allowed()
    {
        Assert.EndsWith(
            "Фактури",
            DocumentStorageUtils.CabinetDirectory(Root, "Фактури"),
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void Metadata_and_parts_sit_inside_the_document_directory()
    {
        var document = Path.Combine(Root, "cabinets", "Invoices", "2026-03-01", "x_y");

        Assert.AreEqual(
            Path.Combine(document, "document.json"),
            DocumentStorageUtils.MetadataFile(document));
        Assert.AreEqual(
            Path.Combine(document, "parts"),
            DocumentStorageUtils.PartsDirectory(document));
    }

    [TestMethod]
    public void A_day_directory_written_with_the_day_format_reads_back_as_that_day()
    {
        var storedAt = new DateTimeOffset(2026, 3, 1, 23, 30, 0, TimeSpan.Zero);
        var name = storedAt.UtcDateTime.ToString(DocumentStorageUtils.DayFormat);

        Assert.AreEqual(new DateOnly(2026, 3, 1), DocumentStorageUtils.DayOf(name));
    }

    [TestMethod]
    public void A_document_directory_name_reads_back_as_the_instant_it_was_written_with()
    {
        var storedAt = new DateTimeOffset(2026, 3, 1, 9, 30, 15, 123, TimeSpan.Zero);
        var id = Guid.CreateVersion7();
        var name =
            $"{storedAt.UtcDateTime.ToString(DocumentStorageUtils.InstantFormat)}_{id}";

        Assert.AreEqual(storedAt, DocumentStorageUtils.StoredAtOf(name));
    }

    [TestMethod]
    public void An_instant_is_read_back_as_UTC_whatever_the_machine_runs_in()
    {
        var name = $"20260301T093015123Z_{Guid.CreateVersion7()}";

        Assert.AreEqual(TimeSpan.Zero, DocumentStorageUtils.StoredAtOf(name)!.Value.Offset);
    }

    [TestMethod]
    public void A_full_path_is_accepted_as_well_as_a_bare_name()
    {
        var path = Path.Combine(Root, "cabinets", "Invoices", "2026-03-01");

        Assert.AreEqual(new DateOnly(2026, 3, 1), DocumentStorageUtils.DayOf(path));
    }
}
