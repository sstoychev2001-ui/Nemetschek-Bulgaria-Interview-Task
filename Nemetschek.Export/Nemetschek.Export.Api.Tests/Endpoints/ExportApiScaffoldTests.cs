using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Nemetschek.Export.Api.Tests.Endpoints;

/// <summary>
/// What the Export service answers today: it starts, it describes itself, and it reports liveness.
/// </summary>
public sealed class ExportApiScaffoldTests : IDisposable
{
    private readonly WebApplicationFactory<Program> application = new();

    private readonly HttpClient client;

    public ExportApiScaffoldTests() => client = application.CreateClient();

    [Fact]
    public async Task The_service_starts_and_reports_liveness()
    {
        var response = await client.GetAsync("/health", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(CancellationToken.None));
    }

    [Fact]
    public async Task The_service_describes_itself_in_OpenAPI()
    {
        var response = await client.GetAsync("/openapi/v1.json", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "Nemetschek.Export.Api",
            await response.Content.ReadAsStringAsync(CancellationToken.None),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task There_is_no_export_endpoint_yet()
    {
        var response = await client.PostAsync("/exports", content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task No_readiness_endpoint_is_published_yet()
    {
        var response = await client.GetAsync("/health/ready", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public void Dispose()
    {
        client.Dispose();
        application.Dispose();
    }
}
