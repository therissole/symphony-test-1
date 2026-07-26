using System.Net;

namespace SymphonyTest1.IntegrationTests.Infrastructure;

[TestFixture]
public class WebClientHostingTests
{
    private IntegrationTestWebAppFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new IntegrationTestWebAppFactory();
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

    [TestCase("/")]
    [TestCase("/languages")]
    [TestCase("/greetings")]
    public async Task ClientRoute_ReturnsBlazorHostPage(string route)
    {
        var response = await _client.GetAsync(route);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
            Assert.That(html, Does.Contain("<title>Symphony Administration</title>"));
            Assert.That(html, Does.Contain("_framework/blazor.webassembly"));
        });
    }
}
