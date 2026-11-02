using System.Net;
using System.Net.Http.Json;
using Nemetschek.Export.Core.Domain.Exports;
using Nemetschek.Export.Core.Clients.Interfaces;

namespace Nemetschek.Export.Api.Tests.Endpoints;

/// <summary>
/// What a client served from another host is allowed to do, against the real service in process.
/// </summary>
public sealed class CorsEndpointTests
{
    private const string Client = "http://localhost:5173";

    [Fact]
    public async Task A_preflight_for_an_export_is_allowed()
    {
        using var application = new ExportApplication(new EmptyArchive());
        using var client = application.CreateClient();

        var preflight = new HttpRequestMessage(HttpMethod.Options, "/exports");
        preflight.Headers.Add("Origin", Client);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await client.SendAsync(preflight, Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", Allowed(response));
        Assert.Contains(
            "POST",
            response.Headers.GetValues("Access-Control-Allow-Methods"),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_download_exposes_the_file_name_so_the_client_need_not_invent_one()
    {
        using var application = new ExportApplication(new EmptyArchive());
        using var client = application.CreateClient();

        var response = await Export(client, Client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "Content-Disposition",
            response.Headers.GetValues("Access-Control-Expose-Headers")
                .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries)),
            StringComparer.OrdinalIgnoreCase);
        Assert.NotNull(response.Content.Headers.ContentDisposition?.FileNameStar);
    }

    [Fact]
    public async Task A_refusal_carries_the_header_too_so_the_client_can_show_the_message()
    {
        using var application = new ExportApplication(new EmptyArchive());
        using var client = application.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/exports")
        {
            Content = JsonContent.Create(new Dictionary<string, string> { ["cabinet"] = "" }),
        };
        request.Headers.Add("Origin", Client);

        var response = await client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("*", Allowed(response));
    }

    [Fact]
    public async Task A_configured_allowlist_names_the_origin_it_allows()
    {
        using var application = new ExportApplication(new EmptyArchive())
        {
            AllowedOrigins = $"{Client}, http://other",
        };
        using var client = application.CreateClient();

        Assert.Equal(Client, Allowed(await Export(client, Client)));
    }

    [Fact]
    public async Task An_origin_outside_a_configured_allowlist_is_given_no_header()
    {
        using var application = new ExportApplication(new EmptyArchive())
        {
            AllowedOrigins = Client,
        };
        using var client = application.CreateClient();

        Assert.Null(Allowed(await Export(client, "http://somewhere.else")));
    }

    private static CancellationToken Token => CancellationToken.None;

    private static Task<HttpResponseMessage> Export(HttpClient client, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/exports")
        {
            Content = JsonContent.Create(new Dictionary<string, string>
            {
                ["cabinet"] = "Invoices",
                ["markerField"] = "Batch",
                ["from"] = "2026-01-01",
                ["to"] = "2026-12-31",
            }),
        };
        request.Headers.Add("Origin", origin);

        return client.SendAsync(request, Token);
    }

    private static string? Allowed(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values)
            ? values.Single()
            : null;

    /// <summary>An Archive holding nothing, so an export is a valid empty zip.</summary>
    private sealed class EmptyArchive : IArchiveClient
    {
        public async IAsyncEnumerable<ArchiveDocument> ReadDocumentsAsync(
            string cabinetName,
            DateOnly from,
            DateOnly to,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask;

            yield break;
        }

        public Task<Stream> OpenPartAsync(
            string cabinetName,
            Guid documentId,
            Guid partId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Stream>(new MemoryStream());

        public Task SetTextIndexValueAsync(
            string cabinetName,
            IReadOnlyCollection<Guid> documentIds,
            string fieldName,
            string value,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
