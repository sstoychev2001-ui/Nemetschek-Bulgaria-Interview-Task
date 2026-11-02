using Microsoft.Extensions.DependencyInjection;
using Nemetschek.Export.Core.Clients;
using Nemetschek.Export.Core.Clients.Interfaces;
using Nemetschek.Export.Core.Services.Exports;

namespace Nemetschek.Export.Infrastructure.Tests;

/// <summary>
/// What <c>AddArchiveClient</c> wires up, and what it refuses to start without.
/// </summary>
public sealed class ServiceRegistrationTests
{
    [Fact]
    public void The_port_is_registered_against_the_HTTP_client()
    {
        var client = Provider("http://archive-api:8080").GetRequiredService<IArchiveClient>();

        Assert.IsType<ArchiveHttpClient>(client);
    }

    [Fact]
    public void An_export_can_be_resolved()
    {
        Assert.NotNull(Provider("http://archive-api:8080").GetRequiredService<ExportService>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_archive_address_stops_startup(string address)
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddArchiveClient(address));
    }

    [Theory]
    [InlineData("/cabinets")]
    [InlineData("not a url")]
    [InlineData("archive-api")]
    public void An_address_that_is_not_absolute_stops_startup(string address)
    {
        var refusal = Assert.Throws<ArgumentException>(
            () => new ServiceCollection().AddArchiveClient(address));

        Assert.Contains("is not an absolute URL", refusal.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://archive-api:8080", "http://archive-api:8080/")]
    [InlineData("http://archive-api:8080/", "http://archive-api:8080/")]
    public void The_address_gets_the_trailing_slash_a_relative_request_needs(
        string supplied,
        string expected)
    {
        var client = Provider(supplied).GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(IArchiveClient));

        Assert.Equal(expected, client.BaseAddress?.AbsoluteUri);
    }

    private static ServiceProvider Provider(string archiveBaseAddress) =>
        new ServiceCollection()
            .AddLogging()
            .AddArchiveClient(archiveBaseAddress)
            .BuildServiceProvider();
}
