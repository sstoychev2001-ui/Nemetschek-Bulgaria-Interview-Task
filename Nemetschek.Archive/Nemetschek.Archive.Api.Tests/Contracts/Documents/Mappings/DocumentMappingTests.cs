using System.Globalization;
using Nemetschek.Archive.Api.Contracts.Documents.Mappings;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Api.Tests.Contracts.Documents.Mappings;

/// <summary>
/// What a stored document looks like on the wire.
/// </summary>
[TestClass]
public sealed class DocumentMappingTests
{
    private static readonly DateTimeOffset StoredAt =
        new(2026, 3, 1, 9, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public void A_document_maps_its_identity_title_and_timestamps()
    {
        var document = Stored() with { ModifiedAt = StoredAt.AddDays(1) };

        var response = document.ToResponse();

        Assert.AreEqual(document.Id, response.Id);
        Assert.AreEqual("invoice-001.txt", response.Title);
        Assert.AreEqual(StoredAt, response.StoredAt);
        Assert.AreEqual(StoredAt.AddDays(1), response.ModifiedAt);
    }

    [TestMethod]
    public void A_part_maps_its_id_name_order_and_size()
    {
        var part = new Part
        {
            Id = Guid.NewGuid(), Name = "Договор София.txt", Order = 2, SizeInBytes = 1234,
        };

        var response = part.ToResponse();

        Assert.AreEqual(part.Id, response.Id);
        Assert.AreEqual("Договор София.txt", response.Name);
        Assert.AreEqual(2, response.Order);
        Assert.AreEqual(1234, response.SizeInBytes);
    }

    [TestMethod]
    public void A_part_never_carries_content_to_the_client()
    {
        Assert.DoesNotContain(
            "Content",
            typeof(Api.Contracts.Documents.Responses.PartResponse)
                .GetProperties().Select(property => property.Name));
    }

    [TestMethod]
    public void Parts_keep_their_stored_order()
    {
        var document = Stored() with
        {
            Parts =
            [
                new Part { Name = "page1.txt", Order = 0 },
                new Part { Name = "page10.txt", Order = 1 },
                new Part { Name = "page2.txt", Order = 2 },
            ],
        };

        CollectionAssert.AreEqual(
            new[] { "page1.txt", "page10.txt", "page2.txt" },
            document.ToResponse().Parts.Select(part => part.Name).ToArray());
    }

    [TestMethod]
    public void Index_values_are_keyed_case_insensitively_so_a_client_can_ask_either_way()
    {
        var response = Stored().ToResponse();

        Assert.IsTrue(response.IndexValues.ContainsKey("batch"));
        Assert.IsTrue(response.IndexValues.ContainsKey("BATCH"));
    }

    [TestMethod]
    public void A_text_value_maps_to_its_string()
    {
        var response = new IndexValue.Text("Фактури Q1").ToResponse();

        Assert.AreEqual(FieldType.Text, response.Type);
        Assert.AreEqual("Фактури Q1", response.Value);
    }

    [TestMethod]
    public void A_number_value_maps_to_the_number_itself_not_a_string()
    {
        var response = new IndexValue.Number(1234.56m).ToResponse();

        Assert.AreEqual(FieldType.Number, response.Type);
        Assert.AreEqual(1234.56m, response.Value);
    }

    [TestMethod]
    public void A_date_value_maps_to_an_ISO_date()
    {
        var response = new IndexValue.Date(new DateOnly(2026, 3, 1)).ToResponse();

        Assert.AreEqual(FieldType.Date, response.Type);
        Assert.AreEqual("2026-03-01", response.Value);
    }

    [TestMethod]
    public void A_date_is_formatted_invariantly_whatever_culture_the_container_runs_in()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            Assert.AreEqual(
                "2026-03-01",
                new IndexValue.Date(new DateOnly(2026, 3, 1)).ToResponse().Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void A_page_maps_its_cabinet_window_and_total()
    {
        var page = new DocumentPage([Stored(), Stored()], 42);

        var response = page.ToResponse("Invoices", number: 3, size: 5);

        Assert.AreEqual("Invoices", response.Cabinet);
        Assert.AreEqual(3, response.Page);
        Assert.AreEqual(5, response.Size);
        Assert.AreEqual(42, response.Total);
        Assert.AreEqual(2, response.Documents.Count);
    }

    [TestMethod]
    public void An_empty_page_maps_to_an_empty_list_rather_than_null()
    {
        var response = new DocumentPage([], 0).ToResponse("Invoices", 1, 20);

        Assert.IsEmpty(response.Documents);
    }

    private static Document Stored() => new()
    {
        Id = Guid.CreateVersion7(),
        CabinetName = "Invoices",
        Title = "invoice-001.txt",
        IndexValues = new Dictionary<string, IndexValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["Batch"] = new IndexValue.Text("sample-material"),
        },
        Parts = [new Part { Name = "page1.txt", Order = 0, SizeInBytes = 8 }],
        StoredAt = StoredAt,
        ModifiedAt = StoredAt,
    };
}
