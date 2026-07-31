using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SymphonyTest1.UnitTests.Architecture;

[TestFixture]
public sealed class AcceptanceArchitectureTests
{
    [Test]
    public void AcceptanceRunner_IsolatesAndRestoresEveryPublishedInfrastructurePort()
    {
        var solutionRoot = FindSolutionRoot();
        var runner = File.ReadAllText(Path.Combine(
            solutionRoot,
            "tests",
            "acceptance",
            "run.ps1"));
        var portMappings = new[]
        {
            (Parameter: "GatewayPort", Environment: "SYMPHONY_API_PORT"),
            (Parameter: "KeycloakPort", Environment: "SYMPHONY_KEYCLOAK_PORT"),
            (Parameter: "PostgresPort", Environment: "SYMPHONY_POSTGRES_PORT"),
            (Parameter: "OpenFgaPort", Environment: "SYMPHONY_OPENFGA_PORT")
        };
        var errors = new List<string>();

        foreach (var mapping in portMappings)
        {
            if (!runner.Contains($"[int]${mapping.Parameter}", StringComparison.Ordinal))
            {
                errors.Add($"run.ps1 must expose {mapping.Parameter}.");
            }

            if (!runner.Contains(
                    $"$previous{mapping.Parameter} = $env:{mapping.Environment}",
                    StringComparison.Ordinal))
            {
                errors.Add($"run.ps1 must preserve {mapping.Environment}.");
            }

            if (!runner.Contains(
                    $"$env:{mapping.Environment} = ${mapping.Parameter}",
                    StringComparison.Ordinal))
            {
                errors.Add($"run.ps1 must assign {mapping.Environment} from {mapping.Parameter}.");
            }

            if (!runner.Contains(
                    $"$env:{mapping.Environment} = $previous{mapping.Parameter}",
                    StringComparison.Ordinal))
            {
                errors.Add($"run.ps1 must restore {mapping.Environment}.");
            }
        }

        Assert.That(errors, Is.Empty);
    }

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
    public void AcceptanceTestCore_IsFeatureNeutral_AndDslAndDriversStayInCapabilityFolders()
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
                "Feature DSLs and protocol drivers belong to their capability folders.");
            Assert.That(coreSource.Any(source => source.Contains("Greetings", StringComparison.Ordinal)), Is.False);
            Assert.That(coreSource.Any(source => source.Contains("Languages", StringComparison.Ordinal)), Is.False);
        });
    }

    [Test]
    public void AcceptanceFixtures_MirrorRequestSlices_AndContainOnlyScenarioLanguage()
    {
        var solutionRoot = FindSolutionRoot();
        var acceptanceFeaturesRoot = Path.Combine(
            solutionRoot,
            "tests",
            "acceptance",
            "symphony-test-1.AcceptanceTests",
            "Features");
        var apiFeaturesRoot = Path.Combine(solutionRoot, "src", "symphony-test-1.Api", "Features");
        var fixtures = Directory.GetFiles(
            acceptanceFeaturesRoot,
            "*AcceptanceTests.cs",
            SearchOption.AllDirectories);
        var forbiddenProtocolDetails = new[]
        {
            "HttpMethod.",
            "ApiResponse",
            ".SendAsync<",
            ".SendForResponseAsync(",
            "\"api/",
            "Microsoft.Playwright",
            ".GetByTestId(",
            ".GetByRole(",
            ".Locator(",
            " record "
        };

        var fixturesWithoutRequestSlices = fixtures
            .Where(path =>
            {
                var relativePath = Path.GetRelativePath(acceptanceFeaturesRoot, path);
                var capability = relativePath.Split(Path.DirectorySeparatorChar)[0];
                var fixtureName = Path.GetFileNameWithoutExtension(path);
                var requestName = fixtureName[..^"AcceptanceTests".Length];
                return !File.Exists(Path.Combine(apiFeaturesRoot, capability, $"{requestName}.cs"));
            })
            .Select(path => Path.GetRelativePath(acceptanceFeaturesRoot, path))
            .ToList();
        var fixturesWithProtocolDetails = fixtures
            .Select(path => new
            {
                Path = Path.GetRelativePath(acceptanceFeaturesRoot, path),
                Source = File.ReadAllText(path)
            })
            .Where(fixture => forbiddenProtocolDetails.Any(detail =>
                fixture.Source.Contains(detail, StringComparison.Ordinal)))
            .Select(fixture => fixture.Path)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(fixturesWithoutRequestSlices, Is.Empty,
                "Every acceptance fixture must mirror one API request slice; cross-request workflows belong in end-to-end tests.");
            Assert.That(fixturesWithProtocolDetails, Is.Empty,
                "Acceptance fixtures may contain scenario language and driver construction only; protocol details belong in protocol drivers and test representations belong in the capability DSL.");
        });
    }

    [Test]
    public void EveryRequestAcceptanceFixture_SpecifiesTheUnauthenticatedBoundary()
    {
        var solutionRoot = FindSolutionRoot();
        var acceptanceFeaturesRoot = Path.Combine(
            solutionRoot,
            "tests",
            "acceptance",
            "symphony-test-1.AcceptanceTests",
            "Features");
        var missingScenarios = Directory.GetFiles(
                acceptanceFeaturesRoot,
                "*AcceptanceTests.cs",
                SearchOption.AllDirectories)
            .Where(path => !File.ReadAllText(path).Contains(
                "An_unauthenticated_person_cannot_",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(acceptanceFeaturesRoot, path))
            .ToList();

        Assert.That(
            missingScenarios,
            Is.Empty,
            "Every request slice must visibly specify its anonymous authentication boundary.");
    }

    [Test]
    public void AcceptanceCapabilities_UseTheRequiredRequestOrientedLayout()
    {
        var solutionRoot = FindSolutionRoot();
        var acceptanceFeaturesRoot = Path.Combine(
            solutionRoot,
            "tests",
            "acceptance",
            "symphony-test-1.AcceptanceTests",
            "Features");
        var apiFeaturesRoot = Path.Combine(solutionRoot, "src", "symphony-test-1.Api", "Features");
        var expectedFolders = new[] { "AcceptanceTests", "Dsl", "ProtocolDrivers" };
        var errors = new List<string>();

        foreach (var capability in new[] { "Languages", "Greetings" })
        {
            var acceptanceCapabilityRoot = Path.Combine(acceptanceFeaturesRoot, capability);
            var actualFolders = Directory.GetDirectories(acceptanceCapabilityRoot)
                .Select(path => Path.GetFileName(path)!)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (!actualFolders.SequenceEqual(expectedFolders.Order(StringComparer.Ordinal)))
            {
                errors.Add($"{capability} must contain exactly: {string.Join(", ", expectedFolders)}.");
            }

            var requestNames = Directory.GetFiles(Path.Combine(apiFeaturesRoot, capability), "*.cs")
                .Select(path => Path.GetFileNameWithoutExtension(path)!)
                .Where(name => !name.EndsWith("Feature", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToArray();
            var fixtureNames = Directory.GetFiles(
                    Path.Combine(acceptanceCapabilityRoot, "AcceptanceTests"),
                    "*AcceptanceTests.cs")
                .Select(path => Path.GetFileNameWithoutExtension(path)!)
                .Select(name => name[..^"AcceptanceTests".Length])
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (!requestNames.SequenceEqual(fixtureNames))
            {
                errors.Add(
                    $"{capability} request slices and acceptance fixtures differ. "
                    + $"Slices: [{string.Join(", ", requestNames)}]; fixtures: [{string.Join(", ", fixtureNames)}].");
            }

            var misplacedFiles = Directory.GetFiles(acceptanceCapabilityRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path =>
                {
                    var relative = Path.GetRelativePath(acceptanceCapabilityRoot, path);
                    return !expectedFolders.Contains(
                        relative.Split(Path.DirectorySeparatorChar)[0],
                        StringComparer.Ordinal);
                })
                .Select(path => Path.GetRelativePath(acceptanceFeaturesRoot, path));
            errors.AddRange(misplacedFiles.Select(path => $"{path} is outside the required folders."));
        }

        Assert.That(errors, Is.Empty);
    }

    [Test]
    public void AcceptanceDsl_ContainsBusinessVocabularyOnly()
    {
        var solutionRoot = FindSolutionRoot();
        var acceptanceFeaturesRoot = Path.Combine(
            solutionRoot,
            "tests",
            "acceptance",
            "symphony-test-1.AcceptanceTests",
            "Features");
        var forbiddenDetails = new[]
        {
            "System.Net",
            "HttpClient",
            "HttpMethod",
            "HttpStatusCode",
            "StatusCodes.",
            "ApiResponse",
            "\"api/",
            "Microsoft.Playwright",
            ".GetByTestId(",
            ".GetByRole(",
            ".Locator("
        };
        var violations = Directory.GetFiles(acceptanceFeaturesRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => path.Split(Path.DirectorySeparatorChar).Contains("Dsl", StringComparer.Ordinal))
            .Select(path => new
            {
                Path = Path.GetRelativePath(acceptanceFeaturesRoot, path),
                Source = File.ReadAllText(path)
            })
            .Where(file => forbiddenDetails.Any(detail =>
                file.Source.Contains(detail, StringComparison.Ordinal)))
            .Select(file => file.Path)
            .ToList();

        Assert.That(
            violations,
            Is.Empty,
            "Capability DSLs must express business intent; HTTP and browser mechanics belong in protocol drivers.");
    }

    [Test]
    public void AcceptanceScenarios_DeclareExactlyOneAction()
    {
        var solutionRoot = FindSolutionRoot();
        var acceptanceFeaturesRoot = Path.Combine(
            solutionRoot,
            "tests",
            "acceptance",
            "symphony-test-1.AcceptanceTests",
            "Features");
        var violations = new List<string>();

        foreach (var fixture in Directory.GetFiles(
                     acceptanceFeaturesRoot,
                     "*AcceptanceTests.cs",
                     SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(fixture);
            var scenarios = Regex.Matches(
                source,
                @"Runner\.RunScenarioAsync\((?<steps>.*?)\);",
                RegexOptions.Singleline);
            foreach (Match scenario in scenarios)
            {
                var actionCount = Regex.Count(
                    scenario.Groups["steps"].Value,
                    @"\bWhen_[A-Za-z0-9_]+");
                if (actionCount != 1)
                {
                    violations.Add(
                        $"{Path.GetRelativePath(acceptanceFeaturesRoot, fixture)} has a scenario with {actionCount} When steps.");
                }
            }
        }

        Assert.That(
            violations,
            Is.Empty,
            "Each acceptance scenario must specify exactly one request action; workflows belong in end-to-end tests.");
    }

    [Test]
    public void AcceptanceSuite_HasNoGenericAuthorizationFixturesOrDrivers()
    {
        var solutionRoot = FindSolutionRoot();
        var acceptanceFeaturesRoot = Path.Combine(
            solutionRoot,
            "tests",
            "acceptance",
            "symphony-test-1.AcceptanceTests",
            "Features");
        var violations = Directory.GetFiles(acceptanceFeaturesRoot, "*Authorization*.cs", SearchOption.AllDirectories)
            .Where(path =>
                path.EndsWith("AcceptanceTests.cs", StringComparison.Ordinal)
                || path.EndsWith("ProtocolDriver.cs", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(acceptanceFeaturesRoot, path))
            .ToList();

        Assert.That(
            violations,
            Is.Empty,
            "Authorization is an outcome of each request slice, not a generic acceptance fixture or protocol driver.");
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
