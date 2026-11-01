using System.Net;
using System.Net.Http.Json;
using Nemetschek.Archive.Api.Contracts.Cabinets.Responses;
using Nemetschek.Archive.Core.Domain.IndexFields;

namespace Nemetschek.Archive.Api.Tests.Endpoints;

/// <summary>
/// <c>GET /cabinets</c>, against the real service in process and the real cabinets.json.
/// </summary>
public sealed class CabinetsEndpointTests : IDisposable
{
    private readonly ArchiveApplication application = new();

    private readonly HttpClient client;

    public CabinetsEndpointTests() => client = application.CreateClient();

    [Fact]
    public async Task Both_deployed_cabinets_are_listed_in_the_order_the_file_declares_them()
    {
        var response = await Read();

        Assert.Equal(["Invoices", "Contracts"], response.Cabinets.Select(cabinet => cabinet.Name));
    }

    [Fact]
    public async Task A_cabinet_carries_its_display_name_and_every_field_of_the_fixture()
    {
        var invoices = await Cabinet("Invoices");

        Assert.Equal("Invoices", invoices.DisplayName);
        Assert.Equal(
            [
                "InvoiceNumber", "Supplier", "Amount", "InvoiceDate", "Batch", "Status",
                "ApprovedBy",
            ],
            invoices.Fields.Select(field => field.Name));
    }

    [Fact]
    public async Task A_field_carries_its_label_type_and_maximum_length()
    {
        var field = await Field("Invoices", "InvoiceNumber");

        Assert.Equal("Invoice number", field.Label);
        Assert.Equal(FieldType.Text, field.Type);
        Assert.Equal(20, field.MaxLength);
    }

    [Theory]
    [InlineData("Invoices", "Amount", FieldType.Number)]
    [InlineData("Invoices", "InvoiceDate", FieldType.Date)]
    [InlineData("Contracts", "SignedOn", FieldType.Date)]
    public async Task A_field_that_is_not_text_says_so(
        string cabinetName,
        string fieldName,
        FieldType expected)
    {
        var field = await Field(cabinetName, fieldName);

        Assert.Equal(expected, field.Type);
        Assert.Null(field.MaxLength);
    }

    [Fact]
    public async Task A_non_writable_field_is_reported_as_such()
    {
        Assert.False((await Field("Invoices", "ApprovedBy")).Writable);
        Assert.False((await Field("Contracts", "Category")).Writable);
    }

    [Fact]
    public async Task The_fields_a_client_may_choose_are_named_by_the_Archive()
    {
        var invoices = await Cabinet("Invoices");

        Assert.Equal(
            ["InvoiceNumber", "Supplier", "Batch", "Status"],
            invoices.Fields.Where(field => field.Choosable).Select(field => field.Name));
    }

    [Theory]
    [InlineData("Amount")]
    [InlineData("InvoiceDate")]
    [InlineData("ApprovedBy")]
    public async Task A_field_that_is_not_writable_text_may_not_be_chosen(string fieldName)
    {
        Assert.False((await Field("Invoices", fieldName)).Choosable);
    }

    [Fact]
    public async Task The_type_is_written_as_a_name_rather_than_a_number()
    {
        var body = await client.GetStringAsync("/cabinets", Token);

        Assert.Contains("""{"name":"Amount","label":"Amount","type":"Number",""", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_route_is_published_in_OpenAPI()
    {
        var document = await client.GetStringAsync("/openapi/v1.json", Token);

        Assert.Contains("\"/cabinets\":", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Listing_the_cabinets_needs_no_storage_and_answers_without_one_document()
    {
        var response = await client.GetAsync("/cabinets", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose()
    {
        client.Dispose();
        application.Dispose();
    }

    private static CancellationToken Token => CancellationToken.None;

    private async Task<CabinetsResponse> Read() =>
        (await client.GetFromJsonAsync<CabinetsResponse>("/cabinets", Token))!;

    private async Task<CabinetResponse> Cabinet(string cabinetName) =>
        Assert.Single((await Read()).Cabinets, cabinet => cabinet.Name == cabinetName);

    private async Task<IndexFieldResponse> Field(string cabinetName, string fieldName) =>
        Assert.Single((await Cabinet(cabinetName)).Fields, field => field.Name == fieldName);
}
