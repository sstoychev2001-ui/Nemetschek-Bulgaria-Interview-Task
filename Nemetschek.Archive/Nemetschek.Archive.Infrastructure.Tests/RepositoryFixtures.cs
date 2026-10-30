namespace Nemetschek.Archive.Infrastructure.Tests;

/// <summary>
/// Locates the repository's given fixtures from a test run.
/// </summary>
// Deliberately the smallest thing that works, and private to this project: the TestSupport library
// that used to hold it was removed, and duplicating four lines beats a shared project that every
// test project has to reference. Api.Tests keeps the fuller RepositoryLayout, because the
// architecture tests also have to read csproj files.
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
