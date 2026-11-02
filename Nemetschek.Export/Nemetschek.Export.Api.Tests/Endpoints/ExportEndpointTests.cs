using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nemetschek.Export.Core.Domain.Exports;
using Nemetschek.Export.Core.Clients.Interfaces;

namespace Nemetschek.Export.Api.Tests.Endpoints;

/// <summary>
/// <c>POST /exports</c>, against the real service in process.
/// </summary>
public sealed class ExportEndpointTests : IDisposable
{
    private readonly StubArchive archive = new();

    private readonly ExportApplication application;

    private readonly HttpClient client;

    public ExportEndpointTests()
    {
        application = new ExportApplication(archive);
        client = application.CreateClient();
    }

    [Fact]
    public async Task An_export_is_answered_with_a_zip_the_caller_can_unpack()
    {
        archive.Add("invoice-001.txt", "scan.pdf");
        archive.Add("delivery", "page1.txt", "page2.txt");
        archive.Add("empty-folder");

        var response = await client.PostAsJsonAsync("/exports", Body(), Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);

        var entries = await Entries(response);
        Assert.Equal(
            ["invoice-001.txt", "delivery/page1.txt", "delivery/page2.txt", "empty-folder/"],
            entries);
    }

    [Fact]
    public async Task The_archive_is_offered_as_a_download_named_after_the_cabinet_and_the_dates()
    {
        var response = await client.PostAsJsonAsync("/exports", Body(), Token);

        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(
            "Invoices-2026-01-01-to-2026-12-31.zip",
            response.Content.Headers.ContentDisposition?.FileName);
    }

    [Fact]
    public async Task Every_exported_document_is_marked_through_the_Archive()
    {
        archive.Add("invoice-001.txt", "scan.pdf");

        await client.PostAsJsonAsync("/exports", Body(), Token);

        Assert.Equal("Batch", Assert.Single(archive.Marked).Field);
        Assert.StartsWith("Exported on ", Assert.Single(archive.Marked).Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cabinet")]
    [InlineData("markerField")]
    [InlineData("from")]
    [InlineData("to")]
    public async Task A_request_missing_a_required_value_is_answered_400(string missing)
    {
        var body = Body();
        body.Remove(missing);

        var response = await client.PostAsJsonAsync("/exports", body, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_to_date_before_the_from_date_is_answered_400()
    {
        var body = Body();
        body["from"] = "2026-12-31";
        body["to"] = "2026-01-01";

        var response = await client.PostAsJsonAsync("/exports", body, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task What_the_Archive_refuses_is_passed_on_as_the_callers_mistake()
    {
        archive.Fail = new HttpRequestException(
            "The Archive answered 404: There is no file cabinet named 'Nonsense'.",
            inner: null,
            HttpStatusCode.NotFound);

        var response = await client.PostAsJsonAsync("/exports", Body(), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        Assert.Equal("The Archive refused the export", problem!.Title);
        Assert.Contains("Nonsense", problem.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreachable_Archive_is_answered_502()
    {
        archive.Fail = new HttpRequestException("the Archive is not there");

        var response = await client.PostAsJsonAsync("/exports", Body(), Token);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        Assert.Equal("The Archive is unreachable", problem!.Title);
    }

    [Fact]
    public async Task The_service_describes_its_export_endpoint_in_OpenAPI()
    {
        var response = await client.GetAsync("/openapi/v1.json", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "/exports",
            await response.Content.ReadAsStringAsync(Token),
            StringComparison.Ordinal);
    }

    public void Dispose()
    {
        client.Dispose();
        application.Dispose();
    }

    private static CancellationToken Token => CancellationToken.None;

    private static Dictionary<string, string> Body() => new()
    {
        ["cabinet"] = "Invoices",
        ["markerField"] = "Batch",
        ["from"] = "2026-01-01",
        ["to"] = "2026-12-31",
    };

    private static async Task<List<string>> Entries(HttpResponseMessage response)
    {
        using var zip = new ZipArchive(
            await response.Content.ReadAsStreamAsync(Token), ZipArchiveMode.Read);

        return [.. zip.Entries.Select(entry => entry.FullName)];
    }

    /// <summary>An <see cref="IArchiveClient"/> that answers from memory.</summary>
    private sealed class StubArchive : IArchiveClient
    {
        private readonly List<ArchiveDocument> documents = [];

        public List<(Guid DocumentId, string Field, string Value)> Marked { get; } = [];

        public Exception? Fail { get; set; }

        public void Add(string title, params string[] partNames) =>
            documents.Add(new ArchiveDocument(
                Guid.CreateVersion7(),
                title,
                [.. partNames.Select(name => new ArchivePart(Guid.CreateVersion7(), name))]));

        public async IAsyncEnumerable<ArchiveDocument> ReadDocumentsAsync(
            string cabinetName,
            DateOnly from,
            DateOnly to,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            if (Fail is { } failure)
            {
                throw failure;
            }

            foreach (var document in documents)
            {
                yield return document;

                await Task.Yield();
            }
        }

        public Task<Stream> OpenPartAsync(
            string cabinetName,
            Guid documentId,
            Guid partId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("content")));

        public Task SetTextIndexValueAsync(
            string cabinetName,
            IReadOnlyCollection<Guid> documentIds,
            string fieldName,
            string value,
            CancellationToken cancellationToken)
        {
            foreach (var documentId in documentIds)
            {
                Marked.Add((documentId, fieldName, value));
            }

            return Task.CompletedTask;
        }
    }
}
