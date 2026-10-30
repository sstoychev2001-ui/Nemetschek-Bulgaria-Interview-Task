using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nemetschek.Archive.Api.Contracts.Documents.Responses;

namespace Nemetschek.Archive.Api.Tests.Endpoints;

/// <summary>
/// <c>GET /cabinets/{cabinet}/documents</c>, against the real service in process.
/// </summary>
// The page cap is run at 5 here on purpose: that is the setting the review demo uses, and it is the
// brief's "the maximum page size is configurable".
public sealed class DocumentsEndpointTests : IDisposable
{
    private readonly ArchiveApplication application = new() { MaxPageSize = 5 };

    private readonly HttpClient client;

    public DocumentsEndpointTests() => client = application.CreateClient();

    [Fact]
    public async Task A_cabinet_with_nothing_in_it_reads_as_an_empty_page()
    {
        var page = await Read("/cabinets/Contracts/documents");

        Assert.Equal("Contracts", page.Cabinet);
        Assert.Equal(0, page.Total);
        Assert.Empty(page.Documents);
    }

    [Fact]
    public async Task An_uploaded_document_can_be_read_back_with_its_index_value()
    {
        await Upload("invoice-001.txt");

        var page = await Read("/cabinets/Invoices/documents");

        var document = Assert.Single(page.Documents);
        Assert.Equal("invoice-001.txt", document.Title);
        Assert.NotEqual(Guid.Empty, document.Id);
        Assert.Equal("sample-material", document.IndexValues["Batch"].Value?.ToString());
        Assert.Equal("invoice-001.txt", Assert.Single(document.Parts).Name);
        Assert.NotEqual(default, document.StoredAt);
    }

    [Fact]
    public async Task A_page_never_exceeds_the_configured_maximum()
    {
        for (var i = 0; i < 7; i++)
        {
            await Upload($"invoice-{i}.txt");
        }

        var page = await Read("/cabinets/Invoices/documents?size=100");

        Assert.Equal(5, page.Documents.Count);
        Assert.Equal(5, page.Size);
        Assert.Equal(7, page.Total);
    }

    [Fact]
    public async Task The_pages_together_hold_every_document_once()
    {
        for (var i = 0; i < 7; i++)
        {
            await Upload($"invoice-{i}.txt");
        }

        var first = await Read("/cabinets/Invoices/documents?page=1&size=5");
        var second = await Read("/cabinets/Invoices/documents?page=2&size=5");

        Assert.Equal(5, first.Documents.Count);
        Assert.Equal(2, second.Documents.Count);
        Assert.Equal(
            7,
            first.Documents.Concat(second.Documents).Select(document => document.Id).Distinct().Count());
    }

    [Fact]
    public async Task A_page_past_the_end_is_empty_but_still_reports_the_total()
    {
        await Upload("invoice-001.txt");

        var page = await Read("/cabinets/Invoices/documents?page=9&size=5");

        Assert.Empty(page.Documents);
        Assert.Equal(1, page.Total);
        Assert.Equal(9, page.Page);
    }

    [Fact]
    public async Task Documents_come_back_oldest_first()
    {
        await Upload("first.txt");
        await Upload("second.txt");
        await Upload("third.txt");

        var page = await Read("/cabinets/Invoices/documents");

        Assert.Equal(
            ["first.txt", "second.txt", "third.txt"],
            page.Documents.Select(document => document.Title));
    }

    [Fact]
    public async Task An_unknown_cabinet_is_answered_404()
    {
        var response = await client.GetAsync("/cabinets/Nonsense/documents", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        Assert.Equal("Unknown file cabinet", problem!.Title);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?size=0")]
    [InlineData("?page=-1")]
    public async Task A_page_window_outside_the_allowed_range_is_answered_400(string query)
    {
        var response = await client.GetAsync($"/cabinets/Invoices/documents{query}", Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_cabinet_is_found_whatever_case_the_caller_used()
    {
        var response = await client.GetAsync("/cabinets/invoices/documents", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // --- health and the API description --------------------------------------------------------

    [Fact]
    public async Task Liveness_answers_without_running_any_check()
    {
        var response = await client.GetAsync("/health", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_answers_healthy_on_a_usable_deployment()
    {
        var response = await client.GetAsync("/health/ready", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task The_service_describes_itself_in_OpenAPI()
    {
        // The brief requires the description to be browsable while the service runs.
        var response = await client.GetAsync("/openapi/v1.json", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = await response.Content.ReadAsStringAsync(Token);
        Assert.Contains("/cabinets/{cabinetName}/uploads", document, StringComparison.Ordinal);
        Assert.Contains("/cabinets/{cabinetName}/documents", document, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        client.Dispose();
        application.Dispose();
    }

    private static CancellationToken Token => CancellationToken.None;

    private async Task<DocumentPageResponse> Read(string path) =>
        (await client.GetFromJsonAsync<DocumentPageResponse>(path, Token))!;

    private async Task Upload(string fileName)
    {
        var body = new MultipartFormDataContent
        {
            { new StringContent("Batch"), "field" },
            { new StringContent("sample-material"), "folderName" },
        };

        var part = new ByteArrayContent(Encoding.UTF8.GetBytes("content"));
        part.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "files",
            FileName = fileName,
        };
        body.Add(part);

        var response = await client.PostAsync("/cabinets/Invoices/uploads", body, Token);
        response.EnsureSuccessStatusCode();
    }
}
