using Nemetschek.Archive.Api.Contracts.Cabinets.Mappings;
using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Api.Tests.Contracts;

/// <summary>
/// What a file cabinet and its index fields look like on the wire.
/// </summary>
public sealed class CabinetMappingTests
{
    [Fact]
    public void A_cabinet_maps_both_of_its_names()
    {
        var response = Cabinet().ToResponse();

        Assert.Equal("Invoices", response.Name);
        Assert.Equal("Incoming invoices", response.DisplayName);
    }

    [Fact]
    public void A_field_maps_its_name_label_type_and_length()
    {
        var field = Assert.Single(
            Cabinet().ToResponse().Fields, mapped => mapped.Name == "InvoiceNumber");

        Assert.Equal("Invoice number", field.Label);
        Assert.Equal(FieldType.Text, field.Type);
        Assert.Equal(20, field.MaxLength);
    }

    [Fact]
    public void A_field_with_no_declared_length_maps_it_as_unbounded()
    {
        var field = Assert.Single(Cabinet().ToResponse().Fields, mapped => mapped.Name == "Amount");

        Assert.Null(field.MaxLength);
    }

    [Fact]
    public void The_fields_keep_the_order_the_cabinet_declares_them_in()
    {
        var response = Cabinet().ToResponse();

        Assert.Equal(
            ["InvoiceNumber", "Amount", "InvoiceDate", "ApprovedBy"],
            response.Fields.Select(field => field.Name));
    }

    [Theory]
    [InlineData("InvoiceNumber", true, true)]
    [InlineData("Amount", true, false)]
    [InlineData("InvoiceDate", true, false)]
    [InlineData("ApprovedBy", false, false)]
    public void A_field_says_whether_it_is_writable_and_whether_it_may_be_chosen(
        string name,
        bool writable,
        bool choosable)
    {
        var field = Assert.Single(Cabinet().ToResponse().Fields, mapped => mapped.Name == name);

        Assert.Equal(writable, field.Writable);
        Assert.Equal(choosable, field.Choosable);
    }

    [Fact]
    public void Every_cabinet_is_mapped_in_the_order_it_was_declared()
    {
        IReadOnlyList<Cabinet> cabinets = [Cabinet(), new() { Name = "Contracts" }];

        var response = cabinets.ToResponse();

        Assert.Equal(["Invoices", "Contracts"], response.Cabinets.Select(cabinet => cabinet.Name));
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
