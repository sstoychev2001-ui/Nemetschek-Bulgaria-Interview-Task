using Nemetschek.Archive.Core.Utils.Documents;

namespace Nemetschek.Archive.Core.Tests.Utils;

/// <summary>
/// The storage layout's names and the two parsers that read them back. No filesystem involved.
/// </summary>
public sealed class DocumentStorageUtilsTests
{
    private const string Root = "/srv/archive";

    [Fact]
    public void Staging_sits_directly_under_the_root()
    {
        Assert.Equal(Path.Combine(Root, "tmp"), DocumentStorageUtils.Staging(Root));
    }

    [Fact]
    public void A_staging_path_is_unique_and_inside_staging()
    {
        var first = DocumentStorageUtils.StagingPath(Root);
        var second = DocumentStorageUtils.StagingPath(Root);

        Assert.NotEqual(first, second);
        Assert.StartsWith(DocumentStorageUtils.Staging(Root), first, StringComparison.Ordinal);
    }

    [Fact]
    public void A_staging_path_takes_the_extension_it_is_given()
    {
        Assert.EndsWith(".json", DocumentStorageUtils.StagingPath(Root, ".json"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_cabinet_sits_under_the_cabinets_directory()
    {
        Assert.Equal(
            Path.Combine(Root, "cabinets", "Invoices"),
            DocumentStorageUtils.CabinetDirectory(Root, "Invoices"));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("nested/name")]
    [InlineData("nested\\name")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("")]
    public void A_cabinet_name_that_is_not_a_single_directory_name_is_refused(string cabinetName)
    {
        var failure = Assert.Throws<ArgumentException>(() =>
            DocumentStorageUtils.CabinetDirectory(Root, cabinetName));

        Assert.Contains(cabinetName, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_unicode_cabinet_name_is_allowed()
    {
        Assert.EndsWith(
            "Фактури",
            DocumentStorageUtils.CabinetDirectory(Root, "Фактури"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Metadata_and_parts_sit_inside_the_document_directory()
    {
        var document = Path.Combine(Root, "cabinets", "Invoices", "2026-03-01", "x_y");

        Assert.Equal(
            Path.Combine(document, "document.json"),
            DocumentStorageUtils.MetadataFile(document));
        Assert.Equal(
            Path.Combine(document, "parts"),
            DocumentStorageUtils.PartsDirectory(document));
    }

    [Fact]
    public void A_day_directory_written_with_the_day_format_reads_back_as_that_day()
    {
        var storedAt = new DateTimeOffset(2026, 3, 1, 23, 30, 0, TimeSpan.Zero);
        var name = storedAt.UtcDateTime.ToString(DocumentStorageUtils.DayFormat);

        Assert.Equal(new DateOnly(2026, 3, 1), DocumentStorageUtils.DayOf(name));
    }

    [Theory]
    [InlineData("tmp")]
    [InlineData("cabinets")]
    [InlineData("2026-3-1")]
    [InlineData("not-a-day")]
    [InlineData("")]
    public void A_directory_that_is_not_a_day_reads_back_as_nothing(string name)
    {
        Assert.Null(DocumentStorageUtils.DayOf(name));
    }

    [Fact]
    public void A_document_directory_name_reads_back_as_the_instant_it_was_written_with()
    {
        var storedAt = new DateTimeOffset(2026, 3, 1, 9, 30, 15, 123, TimeSpan.Zero);
        var id = Guid.CreateVersion7();
        var name =
            $"{storedAt.UtcDateTime.ToString(DocumentStorageUtils.InstantFormat)}_{id}";

        Assert.Equal(storedAt, DocumentStorageUtils.StoredAtOf(name));
    }

    [Fact]
    public void An_instant_is_read_back_as_UTC_whatever_the_machine_runs_in()
    {
        var name = $"20260301T093015123Z_{Guid.CreateVersion7()}";

        Assert.Equal(TimeSpan.Zero, DocumentStorageUtils.StoredAtOf(name)!.Value.Offset);
    }

    [Theory]
    [InlineData("no-separator")]
    [InlineData("_00000000-0000-0000-0000-000000000000")]
    [InlineData("notaninstant_00000000-0000-0000-0000-000000000000")]
    [InlineData("")]
    public void A_name_that_does_not_record_an_instant_reads_back_as_nothing(string name)
    {
        Assert.Null(DocumentStorageUtils.StoredAtOf(name));
    }

    [Fact]
    public void A_full_path_is_accepted_as_well_as_a_bare_name()
    {
        var path = Path.Combine(Root, "cabinets", "Invoices", "2026-03-01");

        Assert.Equal(new DateOnly(2026, 3, 1), DocumentStorageUtils.DayOf(path));
    }
}
