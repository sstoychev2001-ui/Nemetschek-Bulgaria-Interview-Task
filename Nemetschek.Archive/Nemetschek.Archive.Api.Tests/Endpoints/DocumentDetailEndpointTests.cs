using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nemetschek.Archive.Api.Contracts.Documents.Responses;
using Nemetschek.Archive.Api.Contracts.Uploads.Responses;

namespace Nemetschek.Archive.Api.Tests.Endpoints;

/// <summary>
/// The three routes the Export service needs: the date-range query, a part's content, and writing
/// index values back.
/// </summary>
public sealed class DocumentDetailEndpointTests : IDisposable
{
    private readonly ArchiveApplication application = new();

    private readonly HttpClient client;

    public DocumentDetailEndpointTests() => client = application.CreateClient();

    // --- a part's content ----------------------------------------------------------------------

    [Fact]
    public async Task A_parts_content_comes_back_byte_for_byte()
    {
        var document = await Upload("invoice-001.txt");

        var response = await client.GetAsync(Content(document, 0), Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("content of invoice-001.txt", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task The_right_part_of_a_folder_document_comes_back()
    {
        var document = await Upload("delivery/page1.txt", "delivery/page2.txt");

        var response = await client.GetAsync(Content(document, 1), Token);

        Assert.Equal("content of delivery/page2.txt", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Content_is_offered_as_bytes_rather_than_as_text()
    {
        var document = await Upload("invoice-001.txt");

        var response = await client.GetAsync(Content(document, 0), Token);

        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_part_that_is_not_there_is_answered_404()
    {
        var document = await Upload("invoice-001.txt");

        var response = await client.GetAsync(
            $"/cabinets/Invoices/documents/{document.Id}/parts/{Guid.CreateVersion7()}/content",
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        Assert.Equal("Unknown document", problem!.Title);
    }

    [Fact]
    public async Task A_document_that_is_not_there_is_answered_404()
    {
        var response = await client.GetAsync(
            $"/cabinets/Invoices/documents/{Guid.CreateVersion7()}/parts/{Guid.CreateVersion7()}/content",
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_cabinet_is_answered_404_rather_than_searched()
    {
        var response = await client.GetAsync(
            $"/cabinets/Nonsense/documents/{Guid.CreateVersion7()}/parts/{Guid.CreateVersion7()}/content",
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        Assert.Equal("Unknown file cabinet", problem!.Title);
    }

    // --- writing index values back --------------------------------------------------------------

    [Fact]
    public async Task An_index_value_is_set_and_comes_back_on_the_document()
    {
        var document = await Upload("invoice-001.txt");

        var response = await client.PatchAsJsonAsync(
            IndexValues, Marker([document.Id], "Batch", "Exported on 2026-03-01"), Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await Updated(response)).Updated);
        Assert.Equal("Exported on 2026-03-01", await ValueOf(document.Id, "Batch"));
    }

    [Fact]
    public async Task Several_documents_are_set_in_one_request()
    {
        var first = await Upload("invoice-001.txt");
        var second = await Upload("invoice-002.txt");
        var third = await Upload("invoice-003.txt");

        var response = await client.PatchAsJsonAsync(
            IndexValues,
            Marker([first.Id, second.Id, third.Id], "Batch", "Exported on 2026-03-01"),
            Token);

        Assert.Equal(3, (await Updated(response)).Updated);
        Assert.Equal("Exported on 2026-03-01", await ValueOf(first.Id, "Batch"));
        Assert.Equal("Exported on 2026-03-01", await ValueOf(second.Id, "Batch"));
        Assert.Equal("Exported on 2026-03-01", await ValueOf(third.Id, "Batch"));
    }

    [Fact]
    public async Task Only_the_documents_named_are_changed()
    {
        var named = await Upload("invoice-001.txt");
        var untouched = await Upload("invoice-002.txt");

        await client.PatchAsJsonAsync(
            IndexValues, Marker([named.Id], "Batch", "Exported on 2026-03-01"), Token);

        Assert.Equal("sample-material", await ValueOf(untouched.Id, "Batch"));
    }

    [Fact]
    public async Task A_set_value_survives_being_read_again()
    {
        var document = await Upload("invoice-001.txt");

        await client.PatchAsJsonAsync(
            IndexValues, Marker([document.Id], "Batch", "Exported on 2026-03-01"), Token);

        var page = await client.GetFromJsonAsync<DocumentPageResponse>(
            "/cabinets/Invoices/documents", Token);

        Assert.Equal(
            "Exported on 2026-03-01",
            Assert.Single(page!.Documents).IndexValues["Batch"].Value?.ToString());
    }

    [Fact]
    public async Task The_marker_can_be_written_twice_and_the_second_wins()
    {
        var document = await Upload("invoice-001.txt");

        await client.PatchAsJsonAsync(IndexValues, Marker([document.Id], "Batch", "first"), Token);
        await client.PatchAsJsonAsync(IndexValues, Marker([document.Id], "Batch", "second"), Token);

        Assert.Equal("second", await ValueOf(document.Id, "Batch"));
    }

    [Theory]
    [InlineData("ApprovedBy", "is read-only")]
    [InlineData("Nonsense", "has no index field")]
    public async Task A_value_that_breaks_a_field_rule_is_answered_400(string field, string reason)
    {
        var document = await Upload("invoice-001.txt");

        var response = await client.PatchAsJsonAsync(
            IndexValues, Marker([document.Id], field, "anything"), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        Assert.Equal("The index value was refused", problem!.Title);
        Assert.Contains(reason, problem.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_value_too_long_for_its_field_is_answered_400()
    {
        var document = await Upload("invoice-001.txt");

        var response = await client.PatchAsJsonAsync(
            IndexValues, Marker([document.Id], "Status", new string('x', 13)), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_value_that_does_not_parse_as_its_type_is_answered_400()
    {
        var document = await Upload("invoice-001.txt");

        var response = await client.PatchAsJsonAsync(
            IndexValues,
            new
            {
                documentIds = new[] { document.Id },
                values = new { Amount = new { type = "Number", value = "not a number" } },
            },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_id_the_cabinet_does_not_hold_refuses_the_whole_batch()
    {
        // Every document is resolved before any of them is written, so one bad id leaves the good
        // ones untouched rather than half-applying the batch.
        var document = await Upload("invoice-001.txt");

        var response = await client.PatchAsJsonAsync(
            IndexValues,
            Marker([document.Id, Guid.CreateVersion7()], "Batch", "Exported on 2026-03-01"),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("sample-material", await ValueOf(document.Id, "Batch"));
    }

    [Fact]
    public async Task A_request_naming_no_document_is_answered_400()
    {
        var response = await client.PatchAsJsonAsync(
            IndexValues, Marker([], "Batch", "anything"), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_request_naming_no_field_at_all_is_answered_400()
    {
        var document = await Upload("invoice-001.txt");

        var response = await client.PatchAsJsonAsync(
            IndexValues,
            new { documentIds = new[] { document.Id }, values = new Dictionary<string, object>() },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Writing_into_a_cabinet_that_does_not_exist_is_answered_404()
    {
        var response = await client.PatchAsJsonAsync(
            "/cabinets/Nonsense/documents/index-values",
            Marker([Guid.CreateVersion7()], "Batch", "anything"),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        Assert.Equal("Unknown file cabinet", problem!.Title);
    }

    // --- the date range on the query ------------------------------------------------------------

    [Fact]
    public async Task A_date_range_that_covers_today_holds_what_was_just_uploaded()
    {
        await Upload("invoice-001.txt");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var page = await client.GetFromJsonAsync<DocumentPageResponse>(
            $"/cabinets/Invoices/documents?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}", Token);

        Assert.Single(page!.Documents);
    }

    [Fact]
    public async Task A_date_range_in_the_past_holds_nothing()
    {
        await Upload("invoice-001.txt");

        var page = await client.GetFromJsonAsync<DocumentPageResponse>(
            "/cabinets/Invoices/documents?from=2020-01-01&to=2020-12-31", Token);

        Assert.Empty(page!.Documents);
        Assert.Equal(0, page.Total);
    }

    [Fact]
    public async Task A_to_date_before_the_from_date_is_answered_400()
    {
        var response = await client.GetAsync(
            "/cabinets/Invoices/documents?from=2026-12-31&to=2026-01-01", Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        Assert.Equal("The date range is not usable", problem!.Title);
    }

    [Fact]
    public async Task A_time_zone_is_not_a_parameter_any_more_and_is_ignored()
    {
        // Removed on request: dates are read as UTC calendar days. An unknown name in the query
        // string is simply not bound, so the request succeeds rather than being refused — worth
        // pinning, because a client that still sends one must not be given a 400.
        var response = await client.GetAsync(
            "/cabinets/Invoices/documents?timeZone=Mars/Olympus_Mons", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_query_with_no_dates_still_holds_everything()
    {
        await Upload("invoice-001.txt");

        var page = await client.GetFromJsonAsync<DocumentPageResponse>(
            "/cabinets/Invoices/documents", Token);

        Assert.Single(page!.Documents);
    }

    public void Dispose()
    {
        client.Dispose();
        application.Dispose();
    }

    private static CancellationToken Token => CancellationToken.None;

    private const string IndexValues = "/cabinets/Invoices/documents/index-values";

    private static object Marker(Guid[] documentIds, string field, string value) => new
    {
        documentIds,
        values = new Dictionary<string, object> { [field] = new { type = "Text", value } },
    };

    private static async Task<IndexValuesUpdatedResponse> Updated(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<IndexValuesUpdatedResponse>(Token))!;

    /// <summary>The value one index field holds on one stored document, read back over HTTP.</summary>
    private async Task<string?> ValueOf(Guid documentId, string field)
    {
        var page = await client.GetFromJsonAsync<DocumentPageResponse>(
            "/cabinets/Invoices/documents?size=50", Token);

        return page!.Documents.Single(document => document.Id == documentId)
            .IndexValues[field].Value?.ToString();
    }

    private static string Content(DocumentResponse document, int part) =>
        $"/cabinets/Invoices/documents/{document.Id}/parts/{document.Parts[part].Id}/content";

    private async Task<DocumentResponse> Upload(params string[] files)
    {
        var body = new MultipartFormDataContent
        {
            { new StringContent("Batch"), "field" },
            { new StringContent("sample-material"), "folderName" },
        };

        foreach (var path in files)
        {
            var part = new ByteArrayContent(Encoding.UTF8.GetBytes($"content of {path}"));
            part.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "files",
                FileName = path,
            };

            body.Add(part);
        }

        var response = await client.PostAsync("/cabinets/Invoices/uploads", body, Token);
        response.EnsureSuccessStatusCode();

        var report = await response.Content.ReadFromJsonAsync<UploadReportResponse>(Token);
        var stored = report!.Results[0].DocumentId;

        // The report carries part names; the routes under test need the ids, which only the
        // document itself has.
        var page = await client.GetFromJsonAsync<DocumentPageResponse>(
            "/cabinets/Invoices/documents", Token);

        return page!.Documents.Single(document => document.Id == stored);
    }
}
