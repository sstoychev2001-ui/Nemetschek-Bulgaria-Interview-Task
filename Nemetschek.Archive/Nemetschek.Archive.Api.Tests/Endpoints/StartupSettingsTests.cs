namespace Nemetschek.Archive.Api.Tests.Endpoints;

/// <summary>
/// A required setting left at its `appsettings.json` placeholder stops the service starting.
/// </summary>
public sealed class StartupSettingsTests
{
    [Fact]
    public void An_empty_cabinets_file_is_refused_at_startup_naming_the_setting()
    {
        using var application = new ArchiveApplication { CabinetsFile = "" };

        var refusal = Assert.Throws<InvalidOperationException>(() => application.CreateClient());

        Assert.Contains("Archive:CabinetsFile", refusal.Message);
    }

    [Fact]
    public void An_empty_storage_path_is_refused_at_startup_naming_the_setting()
    {
        using var application = new ArchiveApplication { StoragePath = "" };

        var refusal = Assert.Throws<InvalidOperationException>(() => application.CreateClient());

        Assert.Contains("Archive:StoragePath", refusal.Message);
    }

    [Fact]
    public void Whitespace_is_refused_too_so_a_blanked_variable_cannot_pass_as_a_path()
    {
        using var application = new ArchiveApplication { StoragePath = "   " };

        var refusal = Assert.Throws<InvalidOperationException>(() => application.CreateClient());

        Assert.Contains("Archive:StoragePath", refusal.Message);
    }
}
