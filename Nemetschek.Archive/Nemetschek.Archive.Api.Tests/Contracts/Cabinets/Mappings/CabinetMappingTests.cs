using Nemetschek.Archive.Api.Contracts.Cabinets.Mappings;
using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Api.Tests.Contracts.Cabinets.Mappings;

/// <summary>
/// What a file cabinet and its index fields look like on the wire.
/// </summary>
[TestClass]
public sealed class CabinetMappingTests
{
    [TestMethod]
    public void A_cabinet_maps_both_of_its_names()
    {
        var response = Cabinet().ToResponse();

        Assert.AreEqual("Invoices", response.Name);
        Assert.AreEqual("Incoming invoices", response.DisplayName);
    }

    [TestMethod]
    public void A_field_maps_its_name_label_type_and_length()
    {
        var field = Assert.ContainsSingle(
            mapped => mapped.Name == "InvoiceNumber", Cabinet().ToResponse().Fields);

        Assert.AreEqual("Invoice number", field.Label);
        Assert.AreEqual(FieldType.Text, field.Type);
        Assert.AreEqual(20, field.MaxLength);
    }

    [TestMethod]
    public void A_field_with_no_declared_length_maps_it_as_unbounded()
    {
        var field = Assert.ContainsSingle(mapped => mapped.Name == "Amount", Cabinet().ToResponse().Fields);

        Assert.IsNull(field.MaxLength);
    }

    [TestMethod]
    public void The_fields_keep_the_order_the_cabinet_declares_them_in()
    {
        var response = Cabinet().ToResponse();

        CollectionAssert.AreEqual(
            new[] { "InvoiceNumber", "Amount", "InvoiceDate", "ApprovedBy" },
            response.Fields.Select(field => field.Name).ToArray());
    }

    [TestMethod]
    public void Every_cabinet_is_mapped_in_the_order_it_was_declared()
    {
        IReadOnlyList<Cabinet> cabinets = [Cabinet(), new() { Name = "Contracts" }];

        var response = cabinets.ToResponse();

        CollectionAssert.AreEqual(
            new[] { "Invoices", "Contracts" },
            response.Cabinets.Select(cabinet => cabinet.Name).ToArray());
    }

    private static Cabinet Cabinet() => new()
    {
        Name = "Invoices",
        DisplayName = "Incoming invoices",
        Fields =
        [
            Field("InvoiceNumber", "Invoice number", FieldType.Text, 20, writable: true),
            Field("Amount", "Amount", FieldType.Number, null, writable: true),
            Field("InvoiceDate", "Invoice date", FieldType.Date, null, writable: true),
            Field("ApprovedBy", "Approved by", FieldType.Text, 60, writable: false),
        ],
    };

    private static IndexField Field(
        string name,
        string label,
        FieldType type,
        int? maxLength,
        bool writable) =>
        new()
        {
            Name = name,
            Label = label,
            Type = type,
            MaxLength = maxLength,
            Writable = writable,
        };
}
