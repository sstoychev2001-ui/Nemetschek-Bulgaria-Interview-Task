using System.ComponentModel.DataAnnotations;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Tests.Domain.Documents;

/// <summary>
/// The rules <see cref="Document"/> and <see cref="Part"/> declare on themselves, as
/// <c>Validate</c> reports them.
/// </summary>
[TestClass]
public sealed class DocumentValidationTests
{
    [TestMethod]
    public void A_stored_document_without_an_id_is_invalid()
    {
        var document = new Document
        {
            CabinetName = "Invoices",
            Title = "invoice-001.txt",
            IndexValues = Values(),
            Parts = [],
        };

        Assert.Contains("a stored document has no 'id'", Validate(document));
    }

    [TestMethod]
    public void A_stored_document_with_an_id_a_cabinet_and_a_title_is_valid()
    {
        var document = new Document
        {
            Id = Guid.CreateVersion7(),
            CabinetName = "Invoices",
            Title = "invoice-001.txt",
            IndexValues = Values(),
            Parts = [],
        };

        Assert.IsEmpty(Validate(document));
    }

    [TestMethod]
    public void A_stored_part_needs_a_name()
    {
        var part = new Part { Name = "" };

        Assert.Contains("a stored part has no 'name'", Validate(part, allProperties: true));
    }

    private static IReadOnlyDictionary<string, IndexValue> Values() =>
        new Dictionary<string, IndexValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["Batch"] = new IndexValue.Text("sample-material"),
        };

    private static IReadOnlyList<string> Validate(object subject, bool allProperties = false)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            subject, new ValidationContext(subject), results, allProperties);

        return [.. results.Select(result => result.ErrorMessage!)];
    }
}
