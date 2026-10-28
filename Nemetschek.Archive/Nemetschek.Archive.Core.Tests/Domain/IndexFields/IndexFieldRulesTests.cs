using Nemetschek.Archive.TestSupport;
using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Tests.Domain.IndexFields;

/// <summary>
/// One test per field rule in the brief: "Each index field has a type ... maxLength ... writable:
/// whether a client may set the field. The Archive rejects any write to a non-writable field." and
/// "Fields have types and limits, and the Archive enforces them."
/// </summary>
public sealed class IndexFieldRulesTests
{
    // --- writable -----------------------------------------------------------------------------

    [Theory]
    [InlineData("ApprovedBy")]
    public void Write_to_a_non_writable_field_is_refused(string fieldName)
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), fieldName, new IndexValue.Text("anyone"));

        Assert.NotNull(violation);
        Assert.Equal(fieldName, violation.FieldName);
        Assert.Contains("read-only", violation.Reason);
    }

    [Fact]
    public void Write_to_the_non_writable_Contracts_field_is_refused()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Contracts(), "Category", new IndexValue.Text("Lease"));

        Assert.NotNull(violation);
        Assert.Equal("Category", violation.FieldName);
    }

    [Fact]
    public void Write_to_a_writable_field_is_allowed()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Supplier", new IndexValue.Text("Contoso"));

        Assert.Null(violation);
    }

    // --- unknown fields -----------------------------------------------------------------------

    [Fact]
    public void Write_to_a_field_the_cabinet_does_not_have_is_refused()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "SignedOn", new IndexValue.Date(new DateOnly(2026, 3, 1)));

        Assert.NotNull(violation);
        Assert.Contains("no index field", violation.Reason);
    }

    [Fact]
    public void Field_names_are_matched_case_insensitively()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "supplier", new IndexValue.Text("Contoso"));

        Assert.Null(violation);
    }

    // --- types --------------------------------------------------------------------------------

    [Fact]
    public void Text_value_in_a_number_field_is_refused()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Amount", new IndexValue.Text("120.50"));

        Assert.NotNull(violation);
        Assert.Contains("number", violation.Reason);
    }

    [Fact]
    public void Text_value_in_a_date_field_is_refused()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "InvoiceDate", new IndexValue.Text("2026-03-01"));

        Assert.NotNull(violation);
        Assert.Contains("date", violation.Reason);
    }

    [Fact]
    public void Number_value_in_a_text_field_is_refused()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "InvoiceNumber", new IndexValue.Number(42m));

        Assert.NotNull(violation);
    }

    [Fact]
    public void Number_value_in_a_number_field_is_allowed()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Contracts(), "Value", new IndexValue.Number(1234.56m));

        Assert.Null(violation);
    }

    [Fact]
    public void Date_value_in_a_date_field_is_allowed()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Contracts(), "SignedOn", new IndexValue.Date(new DateOnly(2026, 3, 1)));

        Assert.Null(violation);
    }

    // --- maxLength ----------------------------------------------------------------------------

    [Fact]
    public void Text_value_at_the_limit_is_allowed()
    {
        // Status has maxLength 12.
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Status", new IndexValue.Text(new string('x', 12)));

        Assert.Null(violation);
    }

    [Fact]
    public void Text_value_one_over_the_limit_is_refused_and_the_reason_names_the_limit()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Status", new IndexValue.Text(new string('x', 13)));

        Assert.NotNull(violation);
        Assert.Equal("Status", violation.FieldName);
        Assert.Contains("12", violation.Reason);
        Assert.Contains("13", violation.Reason);
    }

    [Fact]
    public void The_sample_folder_name_does_not_fit_the_Status_field()
    {
        // "sample-material" is 15 characters; Status allows 12. The brief's round trip uploads this
        // folder, so a clear refusal here is the documented behaviour, not a truncation.
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Status", new IndexValue.Text("sample-material"));

        Assert.NotNull(violation);
        Assert.Contains("at most 12 characters", violation.Reason);
    }

    [Fact]
    public void The_sample_folder_name_fits_the_Batch_field()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Batch", new IndexValue.Text("sample-material"));

        Assert.Null(violation);
    }

    [Fact]
    public void Unicode_text_is_measured_not_rejected()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Batch", new IndexValue.Text("Фактури Q1"));

        Assert.Null(violation);
    }

    [Fact]
    public void Number_and_date_fields_have_no_length_limit()
    {
        Assert.Null(TestCabinets.Invoices().FindField("Amount")!.MaxLength);
        Assert.Null(TestCabinets.Invoices().FindField("InvoiceDate")!.MaxLength);
    }

    // --- choosing the upload / marker field ---------------------------------------------------

    [Theory]
    [InlineData("InvoiceNumber")]
    [InlineData("Supplier")]
    [InlineData("Batch")]
    [InlineData("Status")]
    public void A_writable_text_field_may_be_chosen(string fieldName)
    {
        Assert.Null(IndexFieldRules.ValidateChosenTextField(TestCabinets.Invoices(), fieldName));
    }

    [Theory]
    [InlineData("Amount", "number")]
    [InlineData("InvoiceDate", "date")]
    public void A_non_text_field_may_not_be_chosen(string fieldName, string expectedTypeWord)
    {
        var violation = IndexFieldRules.ValidateChosenTextField(TestCabinets.Invoices(), fieldName);

        Assert.NotNull(violation);
        Assert.Contains(expectedTypeWord, violation.Reason);
        Assert.Contains("text field is required", violation.Reason);
    }

    [Fact]
    public void A_read_only_text_field_may_not_be_chosen()
    {
        var violation = IndexFieldRules.ValidateChosenTextField(TestCabinets.Invoices(), "ApprovedBy");

        Assert.NotNull(violation);
        Assert.Contains("read-only", violation.Reason);
    }

    [Fact]
    public void An_unknown_field_may_not_be_chosen()
    {
        var violation = IndexFieldRules.ValidateChosenTextField(TestCabinets.Invoices(), "Nonsense");

        Assert.NotNull(violation);
        Assert.Contains("no index field", violation.Reason);
    }

    [Fact]
    public void The_choosable_fields_of_each_cabinet_are_exactly_its_writable_text_fields()
    {
        Assert.Equal(
            ["InvoiceNumber", "Supplier", "Batch", "Status"],
            TestCabinets.Invoices().WritableTextFields.Select(f => f.Name));

        Assert.Equal(
            ["ContractNumber", "Party", "Notes"],
            TestCabinets.Contracts().WritableTextFields.Select(f => f.Name));
    }
}
