using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using SymphonyTest1.Web.Features.Dashboard;
using GreetingsPage = SymphonyTest1.Web.Features.Greetings.ListGreetings;
using LanguagesPage = SymphonyTest1.Web.Features.Languages.ListLanguages;

namespace SymphonyTest1.UnitTests.Architecture;

[TestFixture]
public class WebArchitectureTests
{
    private static readonly string[] CrudSlices =
    [
        "Create",
        "View",
        "Update",
        "Delete"
    ];

    private string _solutionRoot = null!;
    private string _webRoot = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _solutionRoot = FindSolutionRoot();
        _webRoot = Path.Combine(_solutionRoot, "src", "symphony-test-1.Web");
    }

    [Test]
    public void WebProject_DependsOnBrowserLibrariesButNotApiOrPersistence()
    {
        var project = XDocument.Load(Path.Combine(_webRoot, "symphony-test-1.Web.csproj"));
        var projectReferences = project.Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .Where(reference => reference is not null)
            .ToList();
        var packages = project.Descendants("PackageReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .Where(reference => reference is not null)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(projectReferences, Is.Empty,
                "The WASM client must communicate through HTTP rather than reference the API assembly.");
            Assert.That(packages, Does.Contain("MudBlazor"));
            Assert.That(packages, Does.Not.Contain("Dapper"));
            Assert.That(packages, Does.Not.Contain("Npgsql"));
            Assert.That(packages, Does.Not.Contain("FluentValidation"));
        });
    }

    [Test]
    public void GatewayOwnsWebHosting_AndApiDoesNotReferenceWeb()
    {
        var apiProject = XDocument.Load(Path.Combine(
            _solutionRoot,
            "src",
            "symphony-test-1.Api",
            "symphony-test-1.Api.csproj"));
        var gatewayProject = XDocument.Load(Path.Combine(
            _solutionRoot,
            "src",
            "symphony-test-1.Gateway",
            "symphony-test-1.Gateway.csproj"));

        var apiReferences = ProjectReferences(apiProject);
        var gatewayReferences = ProjectReferences(gatewayProject);

        Assert.Multiple(() =>
        {
            Assert.That(apiReferences, Has.None.Contains("symphony-test-1.Web"));
            Assert.That(gatewayReferences, Has.Some.Contains("symphony-test-1.Web"));
        });
    }

    [TestCase("Languages", "Language")]
    [TestCase("Greetings", "Greeting")]
    public void Capability_HasOneComponentPerCrudUseCase(string capability, string entity)
    {
        var featureDirectory = Path.Combine(_webRoot, "Features", capability);
        var expected = CrudSlices
            .Select(operation => Path.Combine(featureDirectory, $"{operation}{entity}.razor"))
            .Append(Path.Combine(featureDirectory, $"List{capability}.razor"));

        Assert.That(expected, Is.All.Exist);
    }

    [Test]
    public void FeatureSlices_DoNotIntroduceSharedClientsServicesOrRepositories()
    {
        var disallowed = Directory.GetFiles(
                Path.Combine(_webRoot, "Features"),
                "*",
                SearchOption.AllDirectories)
            .Where(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                return name.EndsWith("Client", StringComparison.Ordinal)
                    || name.EndsWith("Service", StringComparison.Ordinal)
                    || name.EndsWith("Repository", StringComparison.Ordinal);
            })
            .ToList();

        Assert.That(disallowed, Is.Empty);
    }

    [Test]
    public void ApiCallingSlices_OwnTheirHttpContracts()
    {
        var offenders = Directory.GetFiles(
                Path.Combine(_webRoot, "Features"),
                "*.razor",
                SearchOption.AllDirectories)
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                var exchangesJson = source.Contains("FromJsonAsync", StringComparison.Ordinal)
                    || source.Contains("AsJsonAsync", StringComparison.Ordinal);
                return source.Contains("\"api/", StringComparison.Ordinal)
                    && exchangesJson
                    && !source.Contains("sealed record ", StringComparison.Ordinal);
            })
            .Select(Path.GetFileName)
            .ToList();

        Assert.That(offenders, Is.Empty,
            "A slice that calls the API should declare the response or request shape it consumes.");
    }

    [Test]
    public void WebSource_DoesNotCompileAgainstApiNamespaces()
    {
        var offenders = Directory.GetFiles(_webRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cs" or ".razor")
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains("SymphonyTest1.Api", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(_webRoot, path))
            .ToList();

        Assert.That(offenders, Is.Empty);
    }

    [Test]
    public void AdministrationPages_RequireAuthentication()
    {
        var administrationPages = new[]
        {
            typeof(DashboardPage),
            typeof(LanguagesPage),
            typeof(GreetingsPage)
        };

        var unprotectedPages = administrationPages
            .Where(page => page.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Length == 0)
            .Select(page => page.FullName)
            .ToList();

        Assert.That(unprotectedPages, Is.Empty);
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

    private static List<string> ProjectReferences(XDocument project)
    {
        return project.Descendants("ProjectReference")
            .Select(reference => (string?)reference.Attribute("Include"))
            .Where(reference => reference is not null)
            .Cast<string>()
            .ToList();
    }
}
