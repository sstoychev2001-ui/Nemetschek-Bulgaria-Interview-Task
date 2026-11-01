using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nemetschek.Export.Core.Services.Interfaces;

namespace Nemetschek.Export.Api.Tests.Endpoints;

/// <summary>
/// The real service, in process, with the Archive replaced by a fake.
/// </summary>
internal sealed class ExportApplication(IArchiveClient archive)
    : WebApplicationFactory<Program>
{
    /// <summary>The origins the client may be served from, as `Export:AllowedOrigins` would set them.</summary>
    public string AllowedOrigins { get; init; } = "";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Export:ArchiveBaseAddress", "http://archive-api.invalid:8080");
        builder.UseSetting("Export:AllowedOrigins", AllowedOrigins);
        builder.UseEnvironment("Production");

        builder.ConfigureTestServices(services => services.AddSingleton(archive));
    }
}
