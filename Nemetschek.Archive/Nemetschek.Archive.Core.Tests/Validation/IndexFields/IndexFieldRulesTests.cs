using Nemetschek.Archive.Core.Tests.Utils;
using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Validation.IndexFields;

namespace Nemetschek.Archive.Core.Tests.Validation.IndexFields;

/// <summary>
/// One test per field rule in the brief: "Each index field has a type ... maxLength ... writable:
/// whether a client may set the field. The Archive rejects any write to a non-writable field." and
/// "Fields have types and limits, and the Archive enforces them."
/// </summary>
[TestClass]
public sealed class IndexFieldRulesTests
{
    [TestMethod]
    public void Write_to_the_non_writable_Contracts_field_is_refused()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Contracts(), "Category", new IndexValue.Text("Lease"));

        Assert.IsNotNull(violation);
        Assert.AreEqual("Category", violation.FieldName);
    }

    [TestMethod]
    public void Write_to_a_writable_field_is_allowed()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Supplier", new IndexValue.Text("Contoso"));

        Assert.IsNull(violation);
    }

    [TestMethod]
    public void Write_to_a_field_the_cabinet_does_not_have_is_refused()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "SignedOn", new IndexValue.Date(new DateOnly(2026, 3, 1)));

        Assert.IsNotNull(violation);
        Assert.Contains("no index field", violation.Reason);
    }

    [TestMethod]
    public void Field_names_are_matched_case_insensitively()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "supplier", new IndexValue.Text("Contoso"));

        Assert.IsNull(violation);
    }

    [TestMethod]
    public void Text_value_in_a_number_field_is_refused()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Amount", new IndexValue.Text("120.50"));

        Assert.IsNotNull(violation);
        Assert.Contains("number", violation.Reason);
    }

    [TestMethod]
    public void Text_value_in_a_date_field_is_refused()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "InvoiceDate", new IndexValue.Text("2026-03-01"));

        Assert.IsNotNull(violation);
        Assert.Contains("date", violation.Reason);
    }

    [TestMethod]
    public void Number_value_in_a_text_field_is_refused()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "InvoiceNumber", new IndexValue.Number(42m));

        Assert.IsNotNull(violation);
    }

    [TestMethod]
    public void Number_value_in_a_number_field_is_allowed()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Contracts(), "Value", new IndexValue.Number(1234.56m));

        Assert.IsNull(violation);
    }

    [TestMethod]
    public void Date_value_in_a_date_field_is_allowed()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Contracts(), "SignedOn", new IndexValue.Date(new DateOnly(2026, 3, 1)));

        Assert.IsNull(violation);
    }

    [TestMethod]
    public void Text_value_at_the_limit_is_allowed()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Status", new IndexValue.Text(new string('x', 12)));

        Assert.IsNull(violation);
    }

    [TestMethod]
    public void Text_value_one_over_the_limit_is_refused_and_the_reason_names_the_limit()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Status", new IndexValue.Text(new string('x', 13)));

        Assert.IsNotNull(violation);
        Assert.AreEqual("Status", violation.FieldName);
        Assert.Contains("12", violation.Reason);
        Assert.Contains("13", violation.Reason);
    }

    [TestMethod]
    public void The_sample_folder_name_does_not_fit_the_Status_field()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Status", new IndexValue.Text("sample-material"));

        Assert.IsNotNull(violation);
        Assert.Contains("at most 12 characters", violation.Reason);
    }

    [TestMethod]
    public void The_sample_folder_name_fits_the_Batch_field()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Batch", new IndexValue.Text("sample-material"));

        Assert.IsNull(violation);
    }

    [TestMethod]
    public void Unicode_text_is_measured_not_rejected()
    {
        var violation = IndexFieldRules.ValidateWrite(
            TestCabinets.Invoices(), "Batch", new IndexValue.Text("Фактури Q1"));

        Assert.IsNull(violation);
    }

    [TestMethod]
    public void Number_and_date_fields_have_no_length_limit()
    {
        Assert.IsNull(Field("Amount").MaxLength);
        Assert.IsNull(Field("InvoiceDate").MaxLength);

        static IndexField Field(string name) =>
            TestCabinets.Invoices().Fields.Single(field => field.Name == name);
    }

    [TestMethod]
    public void A_read_only_text_field_may_not_be_chosen()
    {
        var violation = IndexFieldRules.ValidateChosenTextField(TestCabinets.Invoices(), "ApprovedBy");

        Assert.IsNotNull(violation);
        Assert.Contains("read-only", violation.Reason);
    }

    [TestMethod]
    public void An_unknown_field_may_not_be_chosen()
    {
        var violation = IndexFieldRules.ValidateChosenTextField(TestCabinets.Invoices(), "Nonsense");

        Assert.IsNotNull(violation);
        Assert.Contains("no index field", violation.Reason);
    }

    [TestMethod]
    public void The_choosable_fields_of_each_cabinet_are_exactly_its_writable_text_fields()
    {
        CollectionAssert.AreEqual(
            new[] { "InvoiceNumber", "Supplier", "Batch", "Status" },
            Choosable(TestCabinets.Invoices()).ToArray());

        CollectionAssert.AreEqual(
            new[] { "ContractNumber", "Party", "Notes" },
            Choosable(TestCabinets.Contracts()).ToArray());
    }

    private static IEnumerable<string> Choosable(Cabinet cabinet) =>
        cabinet.Fields
            .Where(field => IndexFieldRules.ValidateChosenTextField(cabinet, field.Name) is null)
            .Select(field => field.Name);
}
