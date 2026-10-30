namespace Nemetschek.Archive.Infrastructure.Tests;

/// <summary>
/// Turning a configured path into an absolute one.
/// </summary>
// This is the rule that makes a forgotten volume mount diagnosable: a relative Archive:StoragePath
// resolves against the content root, which inside the image is /app — so it would store documents in
// the container's own layer. README.md records why neither path is defaulted; this pins the
// resolution itself.
public sealed class ResolvePathTests
{
    private static readonly string Base =
        Path.Combine(Path.GetTempPath(), "archive-content-root");

    [Fact]
    public void A_relative_path_resolves_against_the_base_not_the_working_directory()
    {
        var resolved = CabinetsServiceCollectionExtensions.ResolvePath("fixtures/cabinets.json", Base);

        Assert.Equal(Path.Combine(Base, "fixtures", "cabinets.json"), resolved);
    }

    [Fact]
    public void An_absolute_path_is_kept()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "elsewhere", "cabinets.json");

        Assert.Equal(absolute, CabinetsServiceCollectionExtensions.ResolvePath(absolute, Base));
    }

    [Fact]
    public void The_result_is_always_absolute()
    {
        Assert.True(Path.IsPathRooted(
            CabinetsServiceCollectionExtensions.ResolvePath("cabinets.json", Base)));
    }

    [Fact]
    public void A_path_walking_out_of_the_base_is_normalised_rather_than_refused()
    {
        // Deliberately not a security check: this path comes from the deployment's own
        // configuration, not from a user. The untrusted-name rules live in Core.
        var resolved = CabinetsServiceCollectionExtensions.ResolvePath("../outside.json", Base);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(Base, "..", "outside.json")),
            resolved);
        Assert.DoesNotContain("..", resolved, StringComparison.Ordinal);
    }

    [Fact]
    public void A_redundant_segment_is_collapsed()
    {
        Assert.Equal(
            Path.Combine(Base, "cabinets.json"),
            CabinetsServiceCollectionExtensions.ResolvePath("./cabinets.json", Base));
    }
}
