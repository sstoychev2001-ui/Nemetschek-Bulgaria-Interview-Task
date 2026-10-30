using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Nemetschek.Archive.Infrastructure.Cabinets;
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
    private static string FixturePath => RepositoryFixtures.CabinetsFile;

    private static IReadOnlyList<Cabinet> RealCabinets() => CabinetsFileLoader.Load(FixturePath);

    /// <summary>Loads cabinet definitions from a throwaway file holding the given content.</summary>
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
    /// The whole fixture, field by field, stated here rather than compared to another copy of it.
    /// </summary>
    [Theory]
    [InlineData("Invoices", "InvoiceNumber|Invoice number|Text|20|True", "Supplier|Supplier|Text|60|True",
        "Amount|Amount|Number||True", "InvoiceDate|Invoice date|Date||True", "Batch|Batch|Text|100|True",
        "Status|Status|Text|12|True", "ApprovedBy|Approved by|Text|60|False")]
    [InlineData("Contracts", "ContractNumber|Contract number|Text|30|True", "Party|Contract party|Text|100|True",
        "SignedOn|Signed on|Date||True", "Value|Contract value|Number||True", "Notes|Notes|Text|255|True",
        "Category|Category|Text|40|False")]
    public void Every_field_loads_exactly_as_the_fixture_declares_it(
        string cabinetName,
        params string[] expected)
    {
        Assert.Equal(
            expected,
            Find(RealCabinets(), cabinetName).Fields.Select(field =>
                $"{field.Name}|{field.Label}|{field.Type}|{field.MaxLength}|{field.Writable}"));
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
        Assert.Contains(RealCabinets().SelectMany(c => c.Fields), f => !f.Writable);
    }

    [Fact]
    public void The_catalog_finds_cabinets_case_insensitively()
    {
        var catalog = new CabinetCatalog(CabinetsFileLoader.Load(FixturePath));

        Assert.NotNull(catalog.Find("Invoices"));
        Assert.NotNull(catalog.Find("invoices"));
        Assert.NotNull(catalog.Find("INVOICES"));
        Assert.Null(catalog.Find("Nonsense"));
    }

    [Fact]
    public void A_missing_file_is_refused_and_the_message_names_the_path()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-such-cabinets-file.json");

        var ex = Assert.Throws<FileNotFoundException>(() => CabinetsFileLoader.Load(missing));

        Assert.Equal(missing, ex.FileName);
        Assert.Contains(missing, ex.Message);
    }

    [Fact]
    public void Malformed_json_is_refused()
    {
        Assert.Throws<JsonException>(() => LoadFromJson("{ not json"));
    }

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

    [Fact]
    public void An_unknown_field_type_is_refused()
    {
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
