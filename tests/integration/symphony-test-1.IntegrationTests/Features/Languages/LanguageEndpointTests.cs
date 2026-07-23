using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using SymphonyTest1.Api.Features.Languages;
using SymphonyTest1.IntegrationTests.Infrastructure;

namespace SymphonyTest1.IntegrationTests.Features.Languages;

[TestFixture]
public class LanguageEndpointTests
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

    [Test]
    public async Task GetAllLanguages_ReturnsOkWithLanguages()
    {
        // Act
        var response = await _client.GetAsync("/api/languages");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var languages = await response.Content.ReadFromJsonAsync<List<LanguageResponse>>();
        Assert.That(languages, Is.Not.Null);
        Assert.That(languages!.Count, Is.GreaterThan(0));
    }

    [Test]
    public async Task CreateLanguage_ReturnsCreated()
    {
        // Arrange
        var request = new CreateLanguageRequest("French", "fr");

        // Act
        var response = await _client.PostAsJsonAsync("/api/languages", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var language = await response.Content.ReadFromJsonAsync<LanguageResponse>();
        Assert.That(language, Is.Not.Null);
        Assert.That(language!.Name, Is.EqualTo("French"));
        Assert.That(language.Code, Is.EqualTo("fr"));
    }

    [Test]
    public async Task GetLanguageById_WithValidId_ReturnsLanguage()
    {
        // Arrange - create a language first
        var createRequest = new CreateLanguageRequest("German", "de");
        var createResponse = await _client.PostAsJsonAsync("/api/languages", createRequest);
        var createdLanguage = await createResponse.Content.ReadFromJsonAsync<LanguageResponse>();

        // Act
        var response = await _client.GetAsync($"/api/languages/{createdLanguage!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var language = await response.Content.ReadFromJsonAsync<LanguageResponse>();
        Assert.That(language!.Id, Is.EqualTo(createdLanguage.Id));
    }

    [Test]
    public async Task UpdateLanguage_WithValidId_ReturnsOk()
    {
        // Arrange - create a language first
        var createRequest = new CreateLanguageRequest("Italian", "it");
        var createResponse = await _client.PostAsJsonAsync("/api/languages", createRequest);
        var createdLanguage = await createResponse.Content.ReadFromJsonAsync<LanguageResponse>();

        var updateRequest = new UpdateLanguageRequest("Italian Updated", "it");

        // Act
        var response = await _client.PutAsJsonAsync($"/api/languages/{createdLanguage!.Id}", updateRequest);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await response.Content.ReadFromJsonAsync<LanguageResponse>();
        Assert.That(updated!.Name, Is.EqualTo("Italian Updated"));
    }

    [Test]
    public async Task DeleteLanguage_WithValidId_ReturnsNoContent()
    {
        // Arrange - create a language first
        var createRequest = new CreateLanguageRequest("Portuguese", "pt");
        var createResponse = await _client.PostAsJsonAsync("/api/languages", createRequest);
        var createdLanguage = await createResponse.Content.ReadFromJsonAsync<LanguageResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/languages/{createdLanguage!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }
}
