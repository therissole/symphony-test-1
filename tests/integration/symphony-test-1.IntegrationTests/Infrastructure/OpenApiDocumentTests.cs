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
        var administrationOperations = operations
            .Where(operation =>
                operation.Name.Contains("/api/languages", StringComparison.Ordinal)
                || operation.Name.Contains("/api/greetings", StringComparison.Ordinal))
            .ToList();
        var operationsMissingBearerSecurity = administrationOperations
            .Where(operation => !operation.Value.TryGetProperty("security", out _))
            .Select(operation => operation.Name)
            .ToList();
        var operationsMissingUnauthorizedResponse = administrationOperations
            .Where(operation =>
                !operation.Value.GetProperty("responses").TryGetProperty("401", out _))
            .Select(operation => operation.Name)
            .ToList();
        var publicOperations = operations
            .Where(operation =>
                operation.Name.Contains("/api/health", StringComparison.Ordinal)
                || operation.Name.Contains(
                    "/api/authentication/configuration",
                    StringComparison.Ordinal))
            .ToList();

        var schemas = root.GetProperty("components").GetProperty("schemas");
        var securitySchemes = root.GetProperty("components").GetProperty("securitySchemes");
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
        var identifierParametersWithoutUuidFormat = operations
            .Where(operation => operation.Value.TryGetProperty("parameters", out _))
            .SelectMany(operation => operation.Value.GetProperty("parameters")
                .EnumerateArray()
                .Where(parameter =>
                    parameter.GetProperty("name").GetString() is "id" or "languageId")
                .Where(parameter =>
                    !HasStringValue(parameter.GetProperty("schema"), "type", "string")
                    || !HasStringValue(parameter.GetProperty("schema"), "format", "uuid"))
                .Select(parameter =>
                    $"{operation.Name}: {parameter.GetProperty("name").GetString()}"))
            .ToList();
        var queryParametersWithoutLowerCamelCase = operations
            .Where(operation => operation.Value.TryGetProperty("parameters", out _))
            .SelectMany(operation => operation.Value.GetProperty("parameters")
                .EnumerateArray()
                .Where(parameter =>
                    HasStringValue(parameter, "in", "query")
                    && parameter.GetProperty("name").GetString() is { Length: > 0 } name
                    && char.IsUpper(name[0]))
                .Select(parameter =>
                    $"{operation.Name}: {parameter.GetProperty("name").GetString()}"))
            .ToList();
        var identifierPropertiesWithoutTypedSchema = sliceSchemas
            .Where(schema => schema.Value.TryGetProperty("properties", out _))
            .SelectMany(schema => schema.Value.GetProperty("properties")
                .EnumerateObject()
                .Where(property => property.Name is "id" or "languageId")
                .Where(property =>
                {
                    var expectedSchema = property.Name == "id"
                        && schema.Name.Contains("Greeting", StringComparison.Ordinal)
                            ? "GreetingId"
                            : "LanguageId";

                    return !HasStringValue(
                        property.Value,
                        "$ref",
                        $"#/components/schemas/{expectedSchema}");
                })
                .Select(property => $"{schema.Name}.{property.Name}"))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(operations, Has.Count.EqualTo(13));
            Assert.That(missingSummaries, Is.Empty);
            Assert.That(missingDescriptions, Is.Empty);
            Assert.That(missingParameterDescriptions, Is.Empty);
            Assert.That(operationsMissingBearerSecurity, Is.Empty);
            Assert.That(operationsMissingUnauthorizedResponse, Is.Empty);
            Assert.That(
                publicOperations.All(operation =>
                    !operation.Value.TryGetProperty("security", out _)),
                Is.True);
            Assert.That(securitySchemes.TryGetProperty("Bearer", out _), Is.True);
            Assert.That(undocumentedProperties, Is.Empty);
            Assert.That(schemas.TryGetProperty("Request", out _), Is.False);
            Assert.That(schemas.TryGetProperty("Response", out _), Is.False);
            Assert.That(schemas.TryGetProperty("CreateLanguageRequest", out _), Is.True);
            Assert.That(schemas.TryGetProperty("CreateLanguageResponse", out _), Is.True);
            Assert.That(schemas.TryGetProperty("CreateGreetingRequest", out _), Is.True);
            Assert.That(schemas.TryGetProperty("GetHealthResponse", out _), Is.True);
            Assert.That(
                schemas.TryGetProperty("GetAuthenticationConfigurationResponse", out _),
                Is.True);
            AssertUuidSchema(schemas, "LanguageId");
            AssertUuidSchema(schemas, "GreetingId");
            Assert.That(identifierParametersWithoutUuidFormat, Is.Empty);
            Assert.That(queryParametersWithoutLowerCamelCase, Is.Empty);
            Assert.That(identifierPropertiesWithoutTypedSchema, Is.Empty);
        });
    }

    private static void AssertUuidSchema(JsonElement schemas, string schemaName)
    {
        Assert.That(schemas.TryGetProperty(schemaName, out var schema), Is.True);
        Assert.That(HasStringValue(schema, "type", "string"), Is.True);
        Assert.That(HasStringValue(schema, "format", "uuid"), Is.True);
    }

    private static bool HasNonEmptyString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(property.GetString());
    }

    private static bool HasStringValue(
        JsonElement element,
        string propertyName,
        string expected)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && property.GetString() == expected;
    }
}
