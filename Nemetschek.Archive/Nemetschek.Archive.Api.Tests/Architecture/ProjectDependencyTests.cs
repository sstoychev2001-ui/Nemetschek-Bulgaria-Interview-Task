using Nemetschek.Archive.TestSupport;
using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Api.Tests.Architecture;

/// <summary>
/// The dependency rules of the solution, enforced rather than described.
/// </summary>
// The layering is what makes the rest of the brief achievable: Core having no dependencies is what lets
// `dotnet test` run every rule offline with no containers, and the Archive not referencing the Export is
// what keeps the service boundary honest. Splitting the repository into two solutions would NOT enforce
// either — a .sln is only a list of projects for tooling, and a ProjectReference works across solution
// membership. These tests do enforce it.
//
// They live with the Api tests because the Api sits at the top of the dependency chain and so is the one
// project that can see every other.
public sealed class ProjectDependencyTests
{
    private const string Core = "Nemetschek.Archive.Core";
    private const string Infrastructure = "Nemetschek.Archive.Infrastructure";
    private const string Api = "Nemetschek.Archive.Api";

    /// <summary>The only packages `Core` may reference, named one by one.</summary>
    // An abstractions-only package that reaches no disk, clock or socket does not cost `Core` what
    // the rule protects: `dotnet test` still runs every rule offline with no containers. Anything
    // that does reach one of those belongs in Infrastructure, so this is a list and not a pattern —
    // adding to it is a decision, recorded in DECISIONS.md, rather than a formality.
    private static readonly string[] CorePackageAllowlist =
        ["Microsoft.Extensions.Logging.Abstractions"];

    [Fact]
    public void Core_declares_only_allowed_package_references()
    {
        // If this fails, the code that needed the package almost certainly belongs in Infrastructure.
        Assert.Equal(CorePackageAllowlist, RepositoryLayout.PackageReferencesOf(Core));
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
        // Nothing in production code may reference the Api, and nothing but the Api may reference
        // Infrastructure. Test projects are exempt: each one references its own subject.
        foreach (var project in RepositoryLayout.ProductionProjects)
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
    public void Each_test_project_references_the_project_it_tests()
    {
        // A test project named X.Tests that does not reference X is testing nothing, which is easy to do
        // by accident when projects are added or renamed.
        foreach (var testProject in RepositoryLayout.ProjectFiles.Keys
                     .Where(name => name.EndsWith(".Tests", StringComparison.Ordinal)))
        {
            var subject = testProject[..^".Tests".Length];

            Assert.Contains(subject, RepositoryLayout.ProjectReferencesOf(testProject));
        }
    }

    [Fact]
    public void No_production_project_references_a_test_project()
    {
        foreach (var project in RepositoryLayout.ProductionProjects)
        {
            Assert.DoesNotContain(
                RepositoryLayout.ProjectReferencesOf(project),
                reference => reference.EndsWith(".Tests", StringComparison.Ordinal) ||
                             reference.EndsWith(".TestSupport", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void No_Archive_project_references_an_Export_project()
    {
        // The Export service reads the Archive over HTTP and keeps its own client DTOs. Sharing a
        // project between the two would turn an HTTP boundary into a compile-time one.
        foreach (var project in RepositoryLayout.ProjectFiles.Keys
                     .Where(name => name.StartsWith("Nemetschek.Archive", StringComparison.Ordinal)))
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

        foreach (var project in RepositoryLayout.ProjectFiles.Keys)
        {
            Assert.Contains(project, solution, StringComparison.Ordinal);
        }
    }
}
