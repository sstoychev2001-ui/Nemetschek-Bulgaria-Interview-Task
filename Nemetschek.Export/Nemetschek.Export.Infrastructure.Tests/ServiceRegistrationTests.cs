using Microsoft.Extensions.DependencyInjection;
using Nemetschek.Export.Core.Clients;
using Nemetschek.Export.Core.Clients.Interfaces;
using Nemetschek.Export.Core.Services.Exports;

namespace Nemetschek.Export.Infrastructure.Tests;

/// <summary>
/// What <c>AddArchiveClient</c> wires up, and what it refuses to start without.
/// </summary>
[TestClass]
public sealed class ServiceRegistrationTests
{
    [TestMethod]
    public void The_port_is_registered_against_the_HTTP_client()
    {
        var client = Provider("http://archive-api:8080").GetRequiredService<IArchiveClient>();

        Assert.IsInstanceOfType<ArchiveHttpClient>(client);
    }

    [TestMethod]
    public void An_export_can_be_resolved()
    {
        Assert.IsNotNull(Provider("http://archive-api:8080").GetRequiredService<ExportService>());
    }

    private static ServiceProvider Provider(string archiveBaseAddress) =>
        new ServiceCollection()
            .AddLogging()
            .AddArchiveClient(archiveBaseAddress)
            .BuildServiceProvider();
}
