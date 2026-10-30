using System.Globalization;
using Nemetschek.Archive.Api.Contracts.Documents.Mappings;
using Nemetschek.Archive.Core.Domain.Documents;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Api.Tests.Contracts;

/// <summary>
/// What a stored document looks like on the wire.
/// </summary>
public sealed class DocumentMappingTests
{
    private static readonly DateTimeOffset StoredAt =
        new(2026, 3, 1, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void A_document_maps_its_identity_title_and_timestamps()
    {
        var document = Stored() with { ModifiedAt = StoredAt.AddDays(1) };

        var response = document.ToResponse();

        Assert.Equal(document.Id, response.Id);
        Assert.Equal("invoice-001.txt", response.Title);
        Assert.Equal(StoredAt, response.StoredAt);
        Assert.Equal(StoredAt.AddDays(1), response.ModifiedAt);
    }

    [Fact]
    public void A_part_maps_its_id_name_order_and_size()
    {
        var part = new Part
        {
            Id = Guid.NewGuid(), Name = "Договор София.txt", Order = 2, SizeInBytes = 1234,
        };

        var response = part.ToResponse();

        Assert.Equal(part.Id, response.Id);
        Assert.Equal("Договор София.txt", response.Name);
        Assert.Equal(2, response.Order);
        Assert.Equal(1234, response.SizeInBytes);
    }

    [Fact]
    public void A_part_never_carries_content_to_the_client()
    {
        // The response is metadata only: there is no content endpoint yet, and when there is one it
        // will stream rather than inline bytes. See "Missing" in DECISIONS.md.
        Assert.DoesNotContain(
            "Content",
            typeof(Api.Contracts.Documents.Responses.PartResponse)
                .GetProperties().Select(property => property.Name));
    }

    [Fact]
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

        Assert.Equal(
            ["page1.txt", "page10.txt", "page2.txt"],
            document.ToResponse().Parts.Select(part => part.Name));
    }

    [Fact]
    public void Index_values_are_keyed_case_insensitively_so_a_client_can_ask_either_way()
    {
        var response = Stored().ToResponse();

        Assert.True(response.IndexValues.ContainsKey("batch"));
        Assert.True(response.IndexValues.ContainsKey("BATCH"));
    }

    [Fact]
    public void A_text_value_maps_to_its_string()
    {
        var response = new IndexValue.Text("Фактури Q1").ToResponse();

        Assert.Equal(FieldType.Text, response.Type);
        Assert.Equal("Фактури Q1", response.Value);
    }

    [Fact]
    public void A_number_value_maps_to_the_number_itself_not_a_string()
    {
        // Leaving it a decimal keeps the client from having to parse, and keeps a thousands
        // separator from ever appearing in the payload.
        var response = new IndexValue.Number(1234.56m).ToResponse();

        Assert.Equal(FieldType.Number, response.Type);
        Assert.Equal(1234.56m, response.Value);
    }

    [Fact]
    public void A_date_value_maps_to_an_ISO_date()
    {
        var response = new IndexValue.Date(new DateOnly(2026, 3, 1)).ToResponse();

        Assert.Equal(FieldType.Date, response.Type);
        Assert.Equal("2026-03-01", response.Value);
    }

    [Fact]
    public void A_date_is_formatted_invariantly_whatever_culture_the_container_runs_in()
    {
        // InvariantGlobalization is deliberately off for the export's time-zone handling, so the
        // culture is a real variable. An Umm al-Qura calendar would render 1447-08-12 without this.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");

            Assert.Equal(
                "2026-03-01",
                new IndexValue.Date(new DateOnly(2026, 3, 1)).ToResponse().Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void A_page_maps_its_cabinet_window_and_total()
    {
        var page = new DocumentPage([Stored(), Stored()], 42);

        var response = page.ToResponse("Invoices", number: 3, size: 5);

        Assert.Equal("Invoices", response.Cabinet);
        Assert.Equal(3, response.Page);
        Assert.Equal(5, response.Size);
        Assert.Equal(42, response.Total);
        Assert.Equal(2, response.Documents.Count);
    }

    [Fact]
    public void An_empty_page_maps_to_an_empty_list_rather_than_null()
    {
        var response = new DocumentPage([], 0).ToResponse("Invoices", 1, 20);

        Assert.Empty(response.Documents);
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
