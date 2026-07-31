using System.Net;
using System.Net.Http.Json;

namespace SymphonyTest1.IntegrationTests.Infrastructure;

[TestFixture]
public sealed class AuthenticationBoundaryTests
{
    private IntegrationTestWebAppFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new IntegrationTestWebAppFactory(authenticated: false);
        await _factory.StartAsync();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client.Dispose();
        await _factory.StopAsync();
        await _factory.DisposeAsync();
    }

    private const string ResourceId = "11111111-1111-1111-1111-111111111111";

    public static IEnumerable<TestCaseData> AdministrationRequests
    {
        get
        {
            yield return Request(HttpMethod.Get, "/api/languages");
            yield return Request(HttpMethod.Get, $"/api/languages/{ResourceId}");
            yield return Request(HttpMethod.Post, "/api/languages", """{"name":"English","code":"en"}""");
            yield return Request(HttpMethod.Put, $"/api/languages/{ResourceId}", """{"name":"English","code":"en"}""");
            yield return Request(HttpMethod.Delete, $"/api/languages/{ResourceId}");
            yield return Request(HttpMethod.Get, "/api/greetings");
            yield return Request(HttpMethod.Get, $"/api/greetings/{ResourceId}");
            yield return Request(HttpMethod.Get, "/api/greetings/by-language/en");
            yield return Request(HttpMethod.Post, "/api/greetings",
                $$"""{"languageId":"{{ResourceId}}","greetingText":"Hello","formal":false}""");
            yield return Request(HttpMethod.Put, $"/api/greetings/{ResourceId}",
                $$"""{"languageId":"{{ResourceId}}","greetingText":"Hello","formal":false}""");
            yield return Request(HttpMethod.Delete, $"/api/greetings/{ResourceId}");
        }
    }

    [TestCaseSource(nameof(AdministrationRequests))]
    public async Task AdministrationApi_WhenAnonymous_ReturnsUnauthorized(
        HttpMethod method,
        string path,
        string? json)
    {
        using var request = new HttpRequestMessage(method, path);
        if (json is not null)
        {
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        using var response = await _client.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task HealthApi_WhenAnonymous_RemainsPublic()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task AuthenticationConfiguration_WhenAnonymous_ReturnsPublicOidcSettings()
    {
        var response = await _client.GetAsync("/api/authentication/configuration");
        var configuration = await response.Content.ReadFromJsonAsync<AuthenticationConfiguration>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(configuration?.Authority, Is.Not.Empty);
            Assert.That(configuration?.ClientId, Is.EqualTo("symphony-admin"));
        });
    }

    private sealed record AuthenticationConfiguration(string Authority, string ClientId);

    private static TestCaseData Request(HttpMethod method, string path, string? json = null) =>
        new(method, path, json)
        {
            TestName = $"AdministrationApi_WhenAnonymous_ReturnsUnauthorized({method.Method} {path})"
        };
}
