using Nemetschek.Archive.Core.Domain.Cabinets;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;
using Nemetschek.Archive.Core.Validation.IndexFields;

namespace Nemetschek.Archive.TestSupport;

/// <summary>
/// The two cabinets from fixtures/cabinets.json, built in code.
/// </summary>
// Rule tests use these so they do not depend on file IO. The loader is tested separately against the
// real fixture, and one of its tests compares the loaded fields to these — that comparison is what
// keeps the two from drifting apart.
public static class TestCabinets
{
    public static Cabinet Invoices() => new()
    {
        Name = "Invoices",
        DisplayName = "Invoices",
        Fields =
        [
            Text("InvoiceNumber", "Invoice number", 20, writable: true),
            Text("Supplier", "Supplier", 60, writable: true),
            Of("Amount", "Amount", FieldType.Number, writable: true),
            Of("InvoiceDate", "Invoice date", FieldType.Date, writable: true),
            Text("Batch", "Batch", 100, writable: true),
            Text("Status", "Status", 12, writable: true),
            Text("ApprovedBy", "Approved by", 60, writable: false),
        ],
    };

    public static Cabinet Contracts() => new()
    {
        Name = "Contracts",
        DisplayName = "Contracts",
        Fields =
        [
            Text("ContractNumber", "Contract number", 30, writable: true),
            Text("Party", "Contract party", 100, writable: true),
            Of("SignedOn", "Signed on", FieldType.Date, writable: true),
            Of("Value", "Contract value", FieldType.Number, writable: true),
            Text("Notes", "Notes", 255, writable: true),
            Text("Category", "Category", 40, writable: false),
        ],
    };

    private static IndexField Text(string name, string label, int maxLength, bool writable) => new()
    {
        Name = name,
        Label = label,
        Type = FieldType.Text,
        MaxLength = maxLength,
        Writable = writable,
    };

    private static IndexField Of(string name, string label, FieldType type, bool writable) => new()
    {
        Name = name,
        Label = label,
        Type = type,
        Writable = writable,
    };
}
