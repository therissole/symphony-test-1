using System.Xml.Linq;

namespace SymphonyTest1.UnitTests.Architecture;

[TestFixture]
public sealed class AcceptanceArchitectureTests
{
    [Test]
    public void AcceptanceTests_UseOnlyExternalProtocols()
    {
        var solutionRoot = FindSolutionRoot();
        var acceptanceRoot = Path.Combine(
            solutionRoot,
            "tests",
            "acceptance",
            "symphony-test-1.AcceptanceTests");
        var project = XDocument.Load(Path.Combine(
            acceptanceRoot,
            "symphony-test-1.AcceptanceTests.csproj"));

        var projectReferences = project.Descendants("ProjectReference").ToList();
        var packages = project.Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .Where(package => package is not null)
            .Cast<string>()
            .ToList();
        var implementationReferences = Directory.GetFiles(acceptanceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("SymphonyTest1.", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(acceptanceRoot, path))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(projectReferences, Is.Empty,
                "Acceptance tests must run against a deployment rather than reference application projects.");
            Assert.That(packages, Does.Not.Contain("Microsoft.AspNetCore.Mvc.Testing"));
            Assert.That(packages, Does.Not.Contain("Dapper"));
            Assert.That(packages, Does.Not.Contain("Npgsql"));
            Assert.That(implementationReferences, Is.Empty,
                "Acceptance source must not compile against application namespaces or request/response models.");
        });
    }

    [Test]
    public void AcceptanceTestCore_IsFeatureNeutral_AndDslAndDriversStayInRequestSlices()
    {
        var solutionRoot = FindSolutionRoot();
        var acceptanceRoot = Path.Combine(solutionRoot, "tests", "acceptance", "symphony-test-1.AcceptanceTests");
        var coreRoot = Path.Combine(acceptanceRoot, "Core");
        var coreSource = Directory.GetFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToList();
        var legacyRoots = new[] { "Dsl", "Protocol" }
            .Select(directory => Path.Combine(acceptanceRoot, directory))
            .Where(Directory.Exists)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(legacyRoots, Is.Empty,
                "Feature DSLs and protocol drivers belong to their request-level feature slices.");
            Assert.That(coreSource.Any(source => source.Contains("Greetings", StringComparison.Ordinal)), Is.False);
            Assert.That(coreSource.Any(source => source.Contains("Languages", StringComparison.Ordinal)), Is.False);
        });
    }

    private static string FindSolutionRoot()
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (directory.GetFiles("*.slnx").Length > 0)
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find the solution root.");
    }
}
