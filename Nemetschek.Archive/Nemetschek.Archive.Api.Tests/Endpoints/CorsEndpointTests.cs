using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace Nemetschek.Archive.Api.Tests.Endpoints;

/// <summary>
/// What a client served from another host is allowed to do, against the real service in process.
/// </summary>
public sealed class CorsEndpointTests
{
    private const string Client = "http://localhost:5173";

    [Fact]
    public async Task A_cross_origin_read_is_answered_with_the_header_that_lets_it_be_read()
    {
        using var application = new ArchiveApplication();
        using var client = application.CreateClient();

        var response = await Get(client, "/cabinets", Client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("*", Allowed(response));
    }

    [Fact]
    public async Task A_preflight_for_the_batch_write_is_allowed()
    {
        using var application = new ArchiveApplication();
        using var client = application.CreateClient();

        var preflight = new HttpRequestMessage(
            HttpMethod.Options, "/cabinets/Invoices/documents/index-values");
        preflight.Headers.Add("Origin", Client);
        preflight.Headers.Add("Access-Control-Request-Method", "PATCH");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await client.SendAsync(preflight, Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("*", Allowed(response));
        Assert.Contains(
            "PATCH",
            response.Headers.GetValues("Access-Control-Allow-Methods"),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_refusal_carries_the_header_too_so_the_client_can_show_the_message()
    {
        using var application = new ArchiveApplication();
        using var client = application.CreateClient();

        var response = await Get(client, "/cabinets/Nonsense/documents", Client);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("*", Allowed(response));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Token);
        Assert.Equal("Unknown file cabinet", problem!.Title);
    }

    [Fact]
    public async Task A_configured_allowlist_names_the_origin_it_allows()
    {
        using var application = new ArchiveApplication { AllowedOrigins = $"{Client}, http://other" };
        using var client = application.CreateClient();

        Assert.Equal(Client, Allowed(await Get(client, "/cabinets", Client)));
        Assert.Equal("http://other", Allowed(await Get(client, "/cabinets", "http://other")));
    }

    [Fact]
    public async Task An_origin_outside_a_configured_allowlist_is_given_no_header()
    {
        using var application = new ArchiveApplication { AllowedOrigins = Client };
        using var client = application.CreateClient();

        var response = await Get(client, "/cabinets", "http://somewhere.else");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Allowed(response));
    }

    private static CancellationToken Token => CancellationToken.None;

    private static Task<HttpResponseMessage> Get(HttpClient client, string path, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Origin", origin);

        return client.SendAsync(request, Token);
    }

    private static string? Allowed(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values)
            ? values.Single()
            : null;
}
