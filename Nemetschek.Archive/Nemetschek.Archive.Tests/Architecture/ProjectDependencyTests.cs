namespace Nemetschek.Archive.Tests.Architecture;

/// <summary>
/// The dependency rules of the solution, enforced rather than described.
///
/// These matter because the layering is the thing that makes the rest of the brief achievable: Core
/// having no dependencies is what lets `dotnet test` run every rule offline with no containers, and
/// the Archive not referencing the Export is what keeps the service boundary honest. Splitting the
/// repository into two solutions would NOT enforce either — a .sln is only a list of projects for
/// tooling, and a ProjectReference works across solution membership. These tests do enforce it.
/// </summary>
public sealed class ProjectDependencyTests
{
    private const string Core = "Nemetschek.Archive.Core";
    private const string Infrastructure = "Nemetschek.Archive.Infrastructure";
    private const string Api = "Nemetschek.Archive.Api";
    private const string Tests = "Nemetschek.Archive.Tests";

    [Fact]
    public void Core_declares_no_package_references()
    {
        // If this fails, the code that needed the package almost certainly belongs in Infrastructure.
        Assert.Empty(RepositoryLayout.PackageReferencesOf(Core));
    }

    [Fact]
    public void Core_declares_no_project_references()
    {
        Assert.Empty(RepositoryLayout.ProjectReferencesOf(Core));
    }

    [Fact]
    public void Infrastructure_references_only_Core()
    {
        Assert.Equal([Core], RepositoryLayout.ProjectReferencesOf(Infrastructure));
    }

    [Fact]
    public void Api_references_only_Core_and_Infrastructure()
    {
        Assert.Equal([Core, Infrastructure], RepositoryLayout.ProjectReferencesOf(Api));
    }

    [Fact]
    public void Dependencies_point_inward_only()
    {
        // Nothing may reference the Api except the test project, and nothing may reference Infrastructure
        // except the Api and the test project.
        foreach (var (project, _) in RepositoryLayout.ProjectFiles.Where(p => p.Key != Tests))
        {
            var references = RepositoryLayout.ProjectReferencesOf(project);

            Assert.DoesNotContain(Api, references);

            if (project != Api)
            {
                Assert.DoesNotContain(Infrastructure, references);
            }
        }
    }

    [Fact]
    public void No_Archive_project_references_an_Export_project()
    {
        // The Export service reads the Archive over HTTP and keeps its own client DTOs. Sharing a
        // project between the two would turn an HTTP boundary into a compile-time one.
        foreach (var (project, _) in RepositoryLayout.ProjectFiles
                     .Where(p => p.Key.StartsWith("Nemetschek.Archive", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain(
                RepositoryLayout.ProjectReferencesOf(project),
                reference => reference.StartsWith("Nemetschek.Export", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Every_project_in_the_repository_is_in_the_solution()
    {
        // A project missing from the solution still builds locally but is invisible to `dotnet build`
        // and `dotnet test` at the root, which is exactly how the old scaffold's second solution rotted.
        var solution = File.ReadAllText(RepositoryLayout.Path("Nemetschek.sln"));

        foreach (var (project, _) in RepositoryLayout.ProjectFiles)
        {
            Assert.Contains(project, solution, StringComparison.Ordinal);
        }
    }
}
