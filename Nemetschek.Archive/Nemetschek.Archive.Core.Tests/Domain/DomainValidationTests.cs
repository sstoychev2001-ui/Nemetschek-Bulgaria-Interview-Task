using System.ComponentModel.DataAnnotations;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Core.Tests.Domain;

/// <summary>
/// The rules the domain types declare on themselves, as <c>Validate</c> reports them.
/// </summary>
// These run on deserialisation — IJsonOnDeserialized calls Validator.ValidateObject — so they are
// what stands between a hand-edited cabinets.json or document.json and a service that starts up
// believing nonsense. Asserted directly here because going through a file would test the loader
// instead, which Infrastructure.Tests already does.
public sealed class DomainValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void An_index_field_that_allows_no_characters_is_invalid(int maxLength)
    {
        var field = new IndexField
        {
            Name = "Status", Label = "Status", Type = FieldType.Text, MaxLength = maxLength,
        };

        var violation = Assert.Single(Validate(field));

        Assert.Contains("a field must allow at least one character", violation, StringComparison.Ordinal);
        Assert.Contains("Status", violation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(null)]
    public void An_index_field_with_a_usable_limit_is_valid(int? maxLength)
    {
        // null is unbounded, which every number and date field in the fixture relies on.
        var field = new IndexField
        {
            Name = "Status", Label = "Status", Type = FieldType.Text, MaxLength = maxLength,
        };

        Assert.Empty(Validate(field));
    }

    [Fact]
    public void A_stored_document_without_an_id_is_invalid()
    {
        // A document.json read back without an id would otherwise flow into a response as
        // Guid.Empty, which a client cannot ask for again.
        var document = Document.ToStore("Invoices", "invoice-001.txt", Values(), []);

        Assert.Contains(
            "a stored document has no 'id'",
            Validate(document));
    }

    [Fact]
    public void A_stored_document_with_an_id_a_cabinet_and_a_title_is_valid()
    {
        var document = Document.ToStore("Invoices", "invoice-001.txt", Values(), []) with
        {
            Id = Guid.CreateVersion7(),
        };

        Assert.Empty(Validate(document));
    }

    [Theory]
    [InlineData("", "invoice-001.txt", "a stored document has no 'cabinet'")]
    [InlineData("Invoices", "", "a stored document has no 'title'")]
    public void A_stored_document_needs_its_cabinet_and_its_title(
        string cabinetName,
        string title,
        string expected)
    {
        var document = Document.ToStore(cabinetName, title, Values(), []) with
        {
            Id = Guid.CreateVersion7(),
        };

        Assert.Contains(expected, Validate(document, allProperties: true));
    }

    [Fact]
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
