using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Nemetschek.Archive.Infrastructure.Cabinets;
using Nemetschek.Archive.TestSupport;
using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Validation.IndexFields;

using Nemetschek.Archive.Core.Services.Cabinets;

namespace Nemetschek.Archive.Infrastructure.Tests.Cabinets;

/// <summary>
/// The loader, tested against the real fixtures/cabinets.json. No network and no containers: the
/// fixture is in the repository, which is exactly why it can be the thing under test.
/// </summary>
public sealed class CabinetsFileLoaderTests
{
    private static string FixturePath =>
        RepositoryLayout.Path("fixtures", "cabinets.json");

    private static IReadOnlyList<Cabinet> RealCabinets() => CabinetsFileLoader.Load(FixturePath);

    /// <summary>Loads cabinet definitions from a throwaway file holding the given content.</summary>
    // The loader only takes a path, so the rule tests need a file. Deleted in a finally block because
    // most of these calls are expected to throw, and the brief is explicit that an operation must not
    // leave temporary files behind.
    private static IReadOnlyList<Cabinet> LoadFromJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cabinets-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, json);
            return CabinetsFileLoader.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // --- the real fixture ---------------------------------------------------------------------

    [Fact]
    public void The_fixture_declares_exactly_the_two_cabinets_in_the_brief()
    {
        Assert.Equal(["Invoices", "Contracts"], RealCabinets().Select(c => c.Name));
    }

    [Fact]
    public void Display_names_are_loaded()
    {
        foreach (var cabinet in RealCabinets())
        {
            Assert.False(string.IsNullOrWhiteSpace(cabinet.DisplayName));
        }
    }

    /// <summary>
    /// This is the test that keeps <see cref="TestCabinets"/> honest. The rule tests use hand-written
    /// cabinet definitions so they do not depend on file IO; if the fixture and those definitions ever
    /// drift apart, every rule test would still pass while testing the wrong schema. Comparing the
    /// loaded fields to the hand-written ones closes that gap.
    /// </summary>
    [Fact]
    public void The_loaded_fields_match_the_hand_written_definitions_used_by_the_rule_tests()
    {
        var loaded = RealCabinets();

        Assert.Equal(TestCabinets.Invoices().Fields, Find(loaded, "Invoices").Fields);
        Assert.Equal(TestCabinets.Contracts().Fields, Find(loaded, "Contracts").Fields);
    }

    [Fact]
    public void Field_order_follows_the_file()
    {
        Assert.Equal(
            ["InvoiceNumber", "Supplier", "Amount", "InvoiceDate", "Batch", "Status", "ApprovedBy"],
            Find(RealCabinets(), "Invoices").Fields.Select(f => f.Name));
    }

    [Theory]
    [InlineData("Invoices", "InvoiceNumber", FieldType.Text, 20, true)]
    [InlineData("Invoices", "Amount", FieldType.Number, null, true)]
    [InlineData("Invoices", "InvoiceDate", FieldType.Date, null, true)]
    [InlineData("Invoices", "Status", FieldType.Text, 12, true)]
    [InlineData("Invoices", "ApprovedBy", FieldType.Text, 60, false)]
    [InlineData("Contracts", "SignedOn", FieldType.Date, null, true)]
    [InlineData("Contracts", "Notes", FieldType.Text, 255, true)]
    [InlineData("Contracts", "Category", FieldType.Text, 40, false)]
    public void Each_field_loads_with_its_type_limit_and_writability(
        string cabinetName, string fieldName, FieldType type, int? maxLength, bool writable)
    {
        var field = Find(RealCabinets(), cabinetName)
            .Fields.SingleOrDefault(candidate => candidate.Name == fieldName);

        Assert.NotNull(field);
        Assert.Equal(type, field.Type);
        Assert.Equal(maxLength, field.MaxLength);
        Assert.Equal(writable, field.Writable);
    }

    [Fact]
    public void The_fixture_has_read_only_fields_so_the_writability_rule_is_actually_exercised()
    {
        // If the fixture ever lost its read-only fields, the rule would still be implemented but
        // nothing real would depend on it.
        Assert.Contains(RealCabinets().SelectMany(c => c.Fields), f => !f.Writable);
    }

    // --- loading from a catalog ---------------------------------------------------------------

    [Fact]
    public void The_catalog_finds_cabinets_case_insensitively()
    {
        var catalog = new CabinetCatalog(CabinetsFileLoader.Load(FixturePath));

        Assert.NotNull(catalog.Find("Invoices"));
        Assert.NotNull(catalog.Find("invoices"));
        Assert.NotNull(catalog.Find("INVOICES"));
        Assert.Null(catalog.Find("Nonsense"));
    }

    // --- a missing or unreadable file ---------------------------------------------------------

    [Fact]
    public void A_missing_file_is_refused_and_the_message_names_the_path()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-such-cabinets-file.json");

        // The framework's own FileNotFoundException already names the file, so the loader does not
        // pre-check existence or wrap it.
        var ex = Assert.Throws<FileNotFoundException>(() => CabinetsFileLoader.Load(missing));

        Assert.Equal(missing, ex.FileName);
        Assert.Contains(missing, ex.Message);
    }

    [Fact]
    public void Malformed_json_is_refused()
    {
        // Syntax is System.Text.Json's job; the loader assumes well-formed JSON and only validates what
        // the content means, so a JsonException is left to propagate unwrapped.
        Assert.Throws<JsonException>(() => LoadFromJson("{ not json"));
    }

    // --- structural validation ----------------------------------------------------------------
    //
    // Two mechanisms are in play and the tests below pin which does what, so neither can quietly stop
    // working. A reference property that is absent or blank is caught by its [Required] attribute
    // during deserialisation and reported as a ValidationException. An absent 'type' is caught by the
    // serialiser instead, because FieldType is a value type and so can use the `required` keyword.

    [Fact]
    public void A_file_without_a_cabinets_property_is_refused()
    {
        var ex = Assert.Throws<ValidationException>(() => LoadFromJson("{}"));

        Assert.Contains("declares no file cabinets", ex.Message);
    }

    [Fact]
    public void A_file_with_no_cabinets_is_refused()
    {
        var ex = Assert.Throws<ValidationException>(
            () => LoadFromJson("""{ "cabinets": [] }"""));

        Assert.Contains("declares no file cabinets", ex.Message);
    }

    [Theory]
    [InlineData("""{ "cabinets": [ { "displayName": "X", "fields": [] } ] }""", "has no 'name'")]
    [InlineData("""{ "cabinets": [ { "name": "X", "fields": [] } ] }""", "has no 'displayName'")]
    [InlineData("""{ "cabinets": [ { "name": "X", "displayName": "X" } ] }""", "declares no 'fields'")]
    public void A_cabinet_missing_a_required_property_is_refused(string json, string expectedReason)
    {
        var ex = Assert.Throws<ValidationException>(() => LoadFromJson(json));

        Assert.Contains(expectedReason, ex.Message);
    }

    [Theory]
    [InlineData("""{ "cabinets": [ { "name": " ", "displayName": "X", "fields": [] } ] }""", "has no 'name'")]
    [InlineData("""{ "cabinets": [ { "name": "X", "displayName": "", "fields": [] } ] }""", "has no 'displayName'")]
    public void A_cabinet_with_a_blank_required_property_is_refused(string json, string expectedReason)
    {
        var ex = Assert.Throws<ValidationException>(() => LoadFromJson(json));

        Assert.Contains(expectedReason, ex.Message);
    }

    [Fact]
    public void Two_cabinets_with_the_same_name_are_refused()
    {
        var ex = Assert.Throws<ValidationException>(() => LoadFromJson(
            """
            { "cabinets": [
              { "name": "X", "displayName": "X", "fields": [
                { "name": "A", "label": "A", "type": "text", "maxLength": 5 } ] },
              { "name": "x", "displayName": "Other", "fields": [
                { "name": "A", "label": "A", "type": "text", "maxLength": 5 } ] }
            ] }
            """));

        Assert.Contains("Cabinet name X is not unique", ex.Message);
    }

    [Fact]
    public void Two_fields_with_the_same_name_in_one_cabinet_are_refused()
    {
        var ex = Assert.Throws<ValidationException>(() => LoadFromJson(
            """
            { "cabinets": [ { "name": "X", "displayName": "X", "fields": [
              { "name": "A", "label": "A", "type": "text", "maxLength": 5, "writable": true },
              { "name": "a", "label": "Other", "type": "text", "maxLength": 5, "writable": true }
            ] } ] }
            """));

        Assert.Contains("Index field name A is not unique in cabinet X", ex.Message);
    }

    // --- field validation ---------------------------------------------------------------------

    [Fact]
    public void An_unknown_field_type_is_refused()
    {
        // FieldType is an enum with a string converter, so an unknown value cannot survive
        // deserialisation. The message names the path to the offending field.
        var ex = Assert.Throws<JsonException>(() => LoadFromJson(
            """
            { "cabinets": [ { "name": "X", "displayName": "X", "fields": [
              { "name": "A", "label": "A", "type": "boolean", "writable": true }
            ] } ] }
            """));

        Assert.Contains("FieldType", ex.Message);
        Assert.Contains("fields[0].type", ex.Message);
    }

    [Theory]
    [InlineData("""{ "label": "A", "type": "text", "maxLength": 5 }""", "has no 'name'")]
    [InlineData("""{ "name": "A", "type": "text", "maxLength": 5 }""", "has no 'label'")]
    public void A_field_missing_a_required_property_is_refused(string field, string expectedReason)
    {
        var ex = Assert.Throws<ValidationException>(() => LoadFromJson(
            $$"""{ "cabinets": [ { "name": "X", "displayName": "X", "fields": [ {{field}} ] } ] }"""));

        Assert.Contains(expectedReason, ex.Message);
    }

    [Fact]
    public void A_field_without_a_type_is_refused()
    {
        // Type is a value type, so it uses the `required` keyword and the serialiser reports its
        // absence by name — an absent type must not silently default to Text.
        var ex = Assert.Throws<JsonException>(() => LoadFromJson(
            """
            { "cabinets": [ { "name": "X", "displayName": "X", "fields": [
              { "name": "A", "label": "A", "maxLength": 5 }
            ] } ] }
            """));

        Assert.Contains("type", ex.Message);
    }

    [Theory]
    [InlineData("""{ "name": "", "label": "A", "type": "text" }""", "has no 'name'")]
    [InlineData("""{ "name": "A", "label": " ", "type": "text" }""", "has no 'label'")]
    public void A_field_with_a_blank_required_property_is_refused(string field, string expectedReason)
    {
        var ex = Assert.Throws<ValidationException>(() => LoadFromJson(
            $$"""{ "cabinets": [ { "name": "X", "displayName": "X", "fields": [ {{field}} ] } ] }"""));

        Assert.Contains(expectedReason, ex.Message);
    }

    private static Cabinet Find(IReadOnlyList<Cabinet> cabinets, string name) =>
        cabinets.Single(c => c.Name == name);
}
