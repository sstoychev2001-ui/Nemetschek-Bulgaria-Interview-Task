using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Nemetschek.Archive.Infrastructure.Cabinets;
using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Validation.IndexFields;

using Nemetschek.Archive.Core.Services.Cabinets;
using Nemetschek.Archive.Infrastructure.Tests.Utils;

namespace Nemetschek.Archive.Infrastructure.Tests.Cabinets;

/// <summary>
/// The loader, tested against the real fixtures/cabinets.json. No network and no containers: the
/// fixture is in the repository, which is exactly why it can be the thing under test.
/// </summary>
[TestClass]
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

    [TestMethod]
    public void The_fixture_declares_exactly_the_two_cabinets_in_the_brief()
    {
        CollectionAssert.AreEqual(
            new[] { "Invoices", "Contracts" },
            RealCabinets().Select(c => c.Name).ToArray());
    }

    [TestMethod]
    public void Display_names_are_loaded()
    {
        foreach (var cabinet in RealCabinets())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(cabinet.DisplayName));
        }
    }

    /// <summary>
    /// The whole fixture, field by field, stated here rather than compared to another copy of it.
    /// </summary>
    [TestMethod]
    public void Field_order_follows_the_file()
    {
        CollectionAssert.AreEqual(
            new[] { "InvoiceNumber", "Supplier", "Amount", "InvoiceDate", "Batch", "Status", "ApprovedBy" },
            Find(RealCabinets(), "Invoices").Fields.Select(f => f.Name).ToArray());
    }

    [TestMethod]
    public void The_fixture_has_read_only_fields_so_the_writability_rule_is_actually_exercised()
    {
        Assert.Contains(f => !f.Writable, RealCabinets().SelectMany(c => c.Fields));
    }

    [TestMethod]
    public void The_catalog_finds_cabinets_case_insensitively()
    {
        var catalog = new CabinetCatalog(CabinetsFileLoader.Load(FixturePath));

        Assert.IsNotNull(catalog.Find("Invoices"));
        Assert.IsNotNull(catalog.Find("invoices"));
        Assert.IsNotNull(catalog.Find("INVOICES"));
        Assert.IsNull(catalog.Find("Nonsense"));
    }

    [TestMethod]
    public void A_missing_file_is_refused_and_the_message_names_the_path()
    {
        var missing = Path.Combine(Path.GetTempPath(), "no-such-cabinets-file.json");

        var ex = Assert.ThrowsExactly<FileNotFoundException>(() => CabinetsFileLoader.Load(missing));

        Assert.AreEqual(missing, ex.FileName);
        Assert.Contains(missing, ex.Message);
    }

    [TestMethod]
    public void Malformed_json_is_refused()
    {
        Assert.ThrowsExactly<JsonException>(() => LoadFromJson("{ not json"));
    }

    [TestMethod]
    public void A_file_without_a_cabinets_property_is_refused()
    {
        var ex = Assert.ThrowsExactly<ValidationException>(() => LoadFromJson("{}"));

        Assert.Contains("declares no file cabinets", ex.Message);
    }

    [TestMethod]
    public void A_file_with_no_cabinets_is_refused()
    {
        var ex = Assert.ThrowsExactly<ValidationException>(
            () => LoadFromJson("""{ "cabinets": [] }"""));

        Assert.Contains("declares no file cabinets", ex.Message);
    }

    [TestMethod]
    public void Two_cabinets_with_the_same_name_are_refused()
    {
        var ex = Assert.ThrowsExactly<ValidationException>(() => LoadFromJson(
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

    [TestMethod]
    public void Two_fields_with_the_same_name_in_one_cabinet_are_refused()
    {
        var ex = Assert.ThrowsExactly<ValidationException>(() => LoadFromJson(
            """
            { "cabinets": [ { "name": "X", "displayName": "X", "fields": [
              { "name": "A", "label": "A", "type": "text", "maxLength": 5, "writable": true },
              { "name": "a", "label": "Other", "type": "text", "maxLength": 5, "writable": true }
            ] } ] }
            """));

        Assert.Contains("Index field name A is not unique in cabinet X", ex.Message);
    }

    [TestMethod]
    public void An_unknown_field_type_is_refused()
    {
        var ex = Assert.ThrowsExactly<JsonException>(() => LoadFromJson(
            """
            { "cabinets": [ { "name": "X", "displayName": "X", "fields": [
              { "name": "A", "label": "A", "type": "boolean", "writable": true }
            ] } ] }
            """));

        Assert.Contains("FieldType", ex.Message);
        Assert.Contains("fields[0].type", ex.Message);
    }

    [TestMethod]
    public void A_field_without_a_type_is_refused()
    {
        var ex = Assert.ThrowsExactly<JsonException>(() => LoadFromJson(
            """
            { "cabinets": [ { "name": "X", "displayName": "X", "fields": [
              { "name": "A", "label": "A", "maxLength": 5 }
            ] } ] }
            """));

        Assert.Contains("type", ex.Message);
    }

    private static Cabinet Find(IReadOnlyList<Cabinet> cabinets, string name) =>
        cabinets.Single(c => c.Name == name);
}
