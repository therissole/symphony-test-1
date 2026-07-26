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

    [TestCase("/api/languages")]
    [TestCase("/api/greetings")]
    public async Task AdministrationApi_WhenAnonymous_ReturnsUnauthorized(string path)
    {
        var response = await _client.GetAsync(path);

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
}
