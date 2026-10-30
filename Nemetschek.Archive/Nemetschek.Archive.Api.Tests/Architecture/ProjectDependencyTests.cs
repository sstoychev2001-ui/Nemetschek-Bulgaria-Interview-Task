using Nemetschek.Archive.Core.Domain.Documents;

namespace Nemetschek.Archive.Api.Tests.Architecture;

/// <summary>
/// The dependency rules of the solution, enforced rather than described.
/// </summary>
public sealed class ProjectDependencyTests
{
    private const string Core = "Nemetschek.Archive.Core";
    private const string Infrastructure = "Nemetschek.Archive.Infrastructure";
    private const string Api = "Nemetschek.Archive.Api";

    private const string ExportCore = "Nemetschek.Export.Core";
    private const string ExportInfrastructure = "Nemetschek.Export.Infrastructure";
    private const string ExportApi = "Nemetschek.Export.Api";

    /// <summary>The only packages `Core` may reference, named one by one.</summary>
    private static readonly string[] CorePackageAllowlist =
        ["Microsoft.Extensions.Logging.Abstractions"];

    [Fact]
    public void Core_declares_only_allowed_package_references()
    {
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
        foreach (var project in RepositoryLayout.ProjectFiles.Keys
                     .Where(name => name.StartsWith("Nemetschek.Archive", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain(
                RepositoryLayout.ProjectReferencesOf(project),
                reference => reference.StartsWith("Nemetschek.Export", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Export_Core_declares_only_allowed_package_references()
    {
        Assert.Empty(RepositoryLayout.PackageReferencesOf(ExportCore));
    }

    [Fact]
    public void Export_Core_declares_no_project_references()
    {
        Assert.Empty(RepositoryLayout.ProjectReferencesOf(ExportCore));
    }

    [Fact]
    public void Export_Infrastructure_references_only_Export_Core()
    {
        Assert.Equal([ExportCore], RepositoryLayout.ProjectReferencesOf(ExportInfrastructure));
    }

    [Fact]
    public void Export_Api_references_only_Export_Core_and_Export_Infrastructure()
    {
        Assert.Equal(
            [ExportCore, ExportInfrastructure],
            RepositoryLayout.ProjectReferencesOf(ExportApi));
    }

    [Fact]
    public void No_Export_project_references_an_Archive_project()
    {
        foreach (var project in RepositoryLayout.ProjectFiles.Keys
                     .Where(name => name.StartsWith("Nemetschek.Export", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain(
                RepositoryLayout.ProjectReferencesOf(project),
                reference => reference.StartsWith("Nemetschek.Archive", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Export_dependencies_point_inward_only()
    {
        foreach (var project in RepositoryLayout.ProductionProjects
                     .Where(name => name.StartsWith("Nemetschek.Export", StringComparison.Ordinal)))
        {
            var references = RepositoryLayout.ProjectReferencesOf(project);

            Assert.DoesNotContain(ExportApi, references);

            if (project != ExportApi)
            {
                Assert.DoesNotContain(ExportInfrastructure, references);
            }
        }
    }

    [Fact]
    public void Both_services_have_the_same_shape()
    {
        foreach (var suffix in new[] { ".Core", ".Infrastructure", ".Api", ".Core.Tests",
                     ".Infrastructure.Tests", ".Api.Tests" })
        {
            Assert.Contains($"Nemetschek.Archive{suffix}", RepositoryLayout.ProjectFiles.Keys);
            Assert.Contains($"Nemetschek.Export{suffix}", RepositoryLayout.ProjectFiles.Keys);
        }
    }

    [Fact]
    public void Every_project_in_the_repository_is_in_the_solution()
    {
        var solution = File.ReadAllText(RepositoryLayout.Path("Nemetschek.sln"));

        foreach (var project in RepositoryLayout.ProjectFiles.Keys)
        {
            Assert.Contains(project, solution, StringComparison.Ordinal);
        }
    }
}
