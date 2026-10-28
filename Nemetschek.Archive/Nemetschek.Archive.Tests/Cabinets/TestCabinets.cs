using Nemetschek.Archive.Core.Cabinets;

namespace Nemetschek.Archive.Tests.Cabinets;

/// <summary>
/// The two cabinets from Task/fixtures/cabinets.json, hand-written so rule tests do not depend on
/// the loader or on reading a file. The loader is tested separately against the real fixture, which
/// is what keeps these two definitions honest.
/// </summary>
internal static class TestCabinets
{
    public static Cabinet Invoices() => new(
        "Invoices",
        "Invoices",
        [
            new IndexField("InvoiceNumber", "Invoice number", FieldType.Text, 20, Writable: true),
            new IndexField("Supplier", "Supplier", FieldType.Text, 60, Writable: true),
            new IndexField("Amount", "Amount", FieldType.Number, null, Writable: true),
            new IndexField("InvoiceDate", "Invoice date", FieldType.Date, null, Writable: true),
            new IndexField("Batch", "Batch", FieldType.Text, 100, Writable: true),
            new IndexField("Status", "Status", FieldType.Text, 12, Writable: true),
            new IndexField("ApprovedBy", "Approved by", FieldType.Text, 60, Writable: false),
        ]);

    public static Cabinet Contracts() => new(
        "Contracts",
        "Contracts",
        [
            new IndexField("ContractNumber", "Contract number", FieldType.Text, 30, Writable: true),
            new IndexField("Party", "Contract party", FieldType.Text, 100, Writable: true),
            new IndexField("SignedOn", "Signed on", FieldType.Date, null, Writable: true),
            new IndexField("Value", "Contract value", FieldType.Number, null, Writable: true),
            new IndexField("Notes", "Notes", FieldType.Text, 255, Writable: true),
            new IndexField("Category", "Category", FieldType.Text, 40, Writable: false),
        ]);
}
