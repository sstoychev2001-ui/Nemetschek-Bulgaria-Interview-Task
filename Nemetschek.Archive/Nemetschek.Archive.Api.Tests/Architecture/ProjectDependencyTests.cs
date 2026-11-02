using Nemetschek.Archive.Api.Tests.Utils;
namespace Nemetschek.Archive.Api.Tests.Architecture;

/// <summary>
/// The Archive's dependency rules, enforced rather than described. The Export's own rules are the
/// mirror of these, in <c>Nemetschek.Export.Api.Tests/Architecture/</c>.
/// </summary>
[TestClass]
public sealed class ProjectDependencyTests
{
    private const string Prefix = "Nemetschek.Archive";

    private const string Core = "Nemetschek.Archive.Core";
    private const string Infrastructure = "Nemetschek.Archive.Infrastructure";
    private const string Api = "Nemetschek.Archive.Api";

    private static readonly string[] CorePackageAllowlist = ["Microsoft.Extensions.Logging.Abstractions"];

    [TestMethod]
    public void Core_declares_only_allowed_package_references()
    {
        CollectionAssert.AreEqual(CorePackageAllowlist, RepositoryLayout.PackageReferencesOf(Core).ToArray());
    }

    [TestMethod]
    public void Core_declares_no_project_references()
    {
        Assert.IsEmpty(RepositoryLayout.ProjectReferencesOf(Core));
    }

    [TestMethod]
    public void Infrastructure_references_only_Core()
    {
        CollectionAssert.AreEqual(
            new[] { Core },
            RepositoryLayout.ProjectReferencesOf(Infrastructure).ToArray());
    }

    [TestMethod]
    public void Api_references_only_Core_and_Infrastructure()
    {
        CollectionAssert.AreEqual(
            new[] { Core, Infrastructure },
            RepositoryLayout.ProjectReferencesOf(Api).ToArray());
    }

    [TestMethod]
    public void Dependencies_point_inward_only()
    {
        foreach (var project in Projects(RepositoryLayout.ProductionProjects))
        {
            var references = RepositoryLayout.ProjectReferencesOf(project);

            Assert.DoesNotContain(Api, references);

            if (project != Api)
            {
                Assert.DoesNotContain(Infrastructure, references);
            }
        }
    }

    [TestMethod]
    public void Each_test_project_references_the_project_it_tests()
    {
        foreach (var testProject in Projects(RepositoryLayout.ProjectFiles.Keys)
                     .Where(name => name.EndsWith(".Tests", StringComparison.Ordinal)))
        {
            var subject = testProject[..^".Tests".Length];

            Assert.Contains(subject, RepositoryLayout.ProjectReferencesOf(testProject));
        }
    }

    [TestMethod]
    public void No_Archive_project_references_an_Export_project()
    {
        foreach (var project in Projects(RepositoryLayout.ProjectFiles.Keys))
        {
            Assert.DoesNotContain(
                reference => reference.StartsWith("Nemetschek.Export", StringComparison.Ordinal),
                RepositoryLayout.ProjectReferencesOf(project));
        }
    }

    [TestMethod]
    public void The_service_is_built_from_six_projects()
    {
        foreach (var suffix in new[] { ".Core", ".Infrastructure", ".Api", ".Core.Tests",
                     ".Infrastructure.Tests", ".Api.Tests" })
        {
            Assert.Contains($"{Prefix}{suffix}", RepositoryLayout.ProjectFiles.Keys);
        }
    }

    [TestMethod]
    public void Every_project_of_the_service_is_in_the_solution()
    {
        var solution = File.ReadAllText(RepositoryLayout.Path("Nemetschek.sln"));

        foreach (var project in Projects(RepositoryLayout.ProjectFiles.Keys))
        {
            Assert.Contains(project, solution, StringComparison.Ordinal);
        }
    }

    private static IReadOnlyList<string> Projects(IEnumerable<string> names)
    {
        var mine = names
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal))
            .ToList();

        Assert.IsNotEmpty(mine);

        return mine;
    }
}
