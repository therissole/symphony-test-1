using System.Net;
using System.Text.Json;

namespace SymphonyTest1.IntegrationTests.Infrastructure;

[TestFixture]
public class OpenApiDocumentTests
{
    private static readonly HashSet<string> HttpMethods =
    [
        "delete",
        "get",
        "head",
        "options",
        "patch",
        "post",
        "put",
        "trace"
    ];

    private IntegrationTestWebAppFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new IntegrationTestWebAppFactory("Development");
        await _factory.StartAsync();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client?.Dispose();

        if (_factory is not null)
        {
            await _factory.StopAsync();
            await _factory.DisposeAsync();
        }
    }

    [Test]
    public async Task Document_DescribesEverySliceAndUsesDistinctContractSchemas()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await using var content = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(content);
        var root = document.RootElement;

        var operations = root.GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value
                .EnumerateObject()
                .Where(operation => HttpMethods.Contains(operation.Name))
                .Select(operation => new
                {
                    Name = $"{operation.Name.ToUpperInvariant()} {path.Name}",
                    Value = operation.Value
                }))
            .ToList();

        var missingSummaries = operations
            .Where(operation => !HasNonEmptyString(operation.Value, "summary"))
            .Select(operation => operation.Name)
            .ToList();
        var missingDescriptions = operations
            .Where(operation => !HasNonEmptyString(operation.Value, "description"))
            .Select(operation => operation.Name)
            .ToList();
        var missingParameterDescriptions = operations
            .Where(operation => operation.Value.TryGetProperty("parameters", out _))
            .SelectMany(operation => operation.Value.GetProperty("parameters")
                .EnumerateArray()
                .Where(parameter => !HasNonEmptyString(parameter, "description"))
                .Select(parameter => $"{operation.Name}: {parameter.GetProperty("name").GetString()}"))
            .ToList();

        var schemas = root.GetProperty("components").GetProperty("schemas");
        var sliceSchemas = schemas.EnumerateObject()
            .Where(schema =>
                schema.Name is not "ProblemDetails" and not "HttpValidationProblemDetails")
            .ToList();
        var undocumentedProperties = sliceSchemas
            .Where(schema => schema.Value.TryGetProperty("properties", out _))
            .SelectMany(schema => schema.Value.GetProperty("properties")
                .EnumerateObject()
                .Where(property => !HasNonEmptyString(property.Value, "description"))
                .Select(property => $"{schema.Name}.{property.Name}"))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(operations, Has.Count.EqualTo(12));
            Assert.That(missingSummaries, Is.Empty);
            Assert.That(missingDescriptions, Is.Empty);
            Assert.That(missingParameterDescriptions, Is.Empty);
            Assert.That(undocumentedProperties, Is.Empty);
            Assert.That(schemas.TryGetProperty("Request", out _), Is.False);
            Assert.That(schemas.TryGetProperty("Response", out _), Is.False);
            Assert.That(schemas.TryGetProperty("CreateLanguageRequest", out _), Is.True);
            Assert.That(schemas.TryGetProperty("CreateLanguageResponse", out _), Is.True);
            Assert.That(schemas.TryGetProperty("CreateGreetingRequest", out _), Is.True);
            Assert.That(schemas.TryGetProperty("GetHealthResponse", out _), Is.True);
        });
    }

    private static bool HasNonEmptyString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString());
    }
}
