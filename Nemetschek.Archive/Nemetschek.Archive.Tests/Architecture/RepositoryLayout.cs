using System.Xml.Linq;

namespace Nemetschek.Archive.Tests.Architecture;

/// <summary>
/// Locates files in the repository from a test run, so tests can assert on the real csproj files and
/// the real fixtures instead of on copies that can drift.
/// </summary>
internal static class RepositoryLayout
{
    /// <summary>
    /// The repository root, found by walking up from the test assembly until Nemetschek.sln appears.
    /// </summary>
    public static DirectoryInfo Root { get; } = FindRoot();

    /// <summary>The given repository-relative path, as an absolute path.</summary>
    public static string Path(params string[] relativeSegments) =>
        System.IO.Path.Combine([Root.FullName, .. relativeSegments]);

    /// <summary>Every project file in the repository, keyed by project name.</summary>
    public static IReadOnlyDictionary<string, string> ProjectFiles { get; } =
        Directory.EnumerateFiles(Root.FullName, "*.csproj", SearchOption.AllDirectories)
            .ToDictionary(
                p => System.IO.Path.GetFileNameWithoutExtension(p),
                p => p,
                StringComparer.Ordinal);

    /// <summary>The names of the projects a project declares a ProjectReference to.</summary>
    public static IReadOnlyList<string> ProjectReferencesOf(string projectName) =>
        ItemsOf(projectName, "ProjectReference")
            .Select(System.IO.Path.GetFileNameWithoutExtension)
            .Select(name => name!)
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>The ids of the NuGet packages a project declares a PackageReference to.</summary>
    public static IReadOnlyList<string> PackageReferencesOf(string projectName) =>
        ItemsOf(projectName, "PackageReference")
            .Order(StringComparer.Ordinal)
            .ToList();

    private static IEnumerable<string> ItemsOf(string projectName, string itemName)
    {
        Assert.True(
            ProjectFiles.ContainsKey(projectName),
            $"No project named '{projectName}' found under {Root.FullName}.");

        return XDocument.Load(ProjectFiles[projectName])
            .Descendants(itemName)
            .Select(e => (string?)e.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!.Replace('\\', System.IO.Path.DirectorySeparatorChar));
    }

    private static DirectoryInfo FindRoot()
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
