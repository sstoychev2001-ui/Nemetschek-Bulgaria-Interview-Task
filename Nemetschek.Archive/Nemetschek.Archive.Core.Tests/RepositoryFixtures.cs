namespace Nemetschek.Archive.Core.Tests;

/// <summary>
/// Locates the repository's given fixtures from a test run.
/// </summary>
internal static class RepositoryFixtures
{
    /// <summary>The given <c>fixtures/cabinets.json</c>.</summary>
    public static string CabinetsFile => Path.Combine(Root().FullName, "fixtures", "cabinets.json");

    private static DirectoryInfo Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (dir.EnumerateFiles("Nemetschek.sln").Any())
            {
                return dir;
            }
        }

        throw new InvalidOperationException(
            $"Could not find Nemetschek.sln above {AppContext.BaseDirectory}.");
    }
}
