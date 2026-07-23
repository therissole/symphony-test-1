using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SymphonyTest1.Api.Features.Languages;
using SymphonyTest1.IntegrationTests.Infrastructure;

namespace SymphonyTest1.IntegrationTests.Features.Languages;

[TestFixture]
public class LanguageSliceTests
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
        _client?.Dispose();

        if (_factory is not null)
        {
            await _factory.StopAsync();
            await _factory.DisposeAsync();
        }
    }

    [Test]
    public async Task ListLanguages_ReturnsSeededLanguages()
    {
        var response = await _client.GetAsync("/api/languages");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var languages = await response.Content.ReadFromJsonAsync<List<ListLanguages.Response>>();
        Assert.That(languages, Is.Not.Null);
        Assert.That(languages, Has.Some.Property(nameof(ListLanguages.Response.Code)).EqualTo("en"));
    }

    [Test]
    public async Task GetLanguage_WhenLanguageDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/languages/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CreateLanguage_WithValidRequest_ReturnsCreatedLanguage()
    {
        var request = new CreateLanguage.Request("French", "fr");

        var response = await _client.PostAsJsonAsync("/api/languages", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location?.ToString(), Does.StartWith("/api/languages/"));

        var language = await response.Content.ReadFromJsonAsync<CreateLanguage.Response>();
        Assert.Multiple(() =>
        {
            Assert.That(language, Is.Not.Null);
            Assert.That(language!.Name, Is.EqualTo("French"));
            Assert.That(language.Code, Is.EqualTo("fr"));
        });
    }

    [Test]
    public async Task CreateLanguage_WithInvalidRequest_ReturnsValidationProblem()
    {
        var request = new CreateLanguage.Request("", "");

        var response = await _client.PostAsJsonAsync("/api/languages", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Errors, Does.ContainKey("name"));
            Assert.That(problem.Errors, Does.ContainKey("code"));
        });
    }

    [Test]
    public async Task CreateLanguage_WithDuplicateCode_ReturnsConflict()
    {
        var request = new CreateLanguage.Request("Another English", "en");

        var response = await _client.PostAsJsonAsync("/api/languages", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem?.Title, Is.EqualTo("Language already exists"));
    }

    [Test]
    public async Task GetAndUpdateLanguage_WithExistingId_ReturnsUpdatedLanguage()
    {
        var created = await CreateLanguageAsync("Italian", "it");

        var getResponse = await _client.GetAsync($"/api/languages/{created.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var retrieved = await getResponse.Content.ReadFromJsonAsync<GetLanguage.Response>();
        Assert.That(retrieved?.Id, Is.EqualTo(created.Id));

        var updateRequest = new UpdateLanguage.Request("Italiano", "it");
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/languages/{created.Id}",
            updateRequest);

        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await updateResponse.Content.ReadFromJsonAsync<UpdateLanguage.Response>();
        Assert.That(updated?.Name, Is.EqualTo("Italiano"));
    }

    [Test]
    public async Task UpdateLanguage_WhenLanguageDoesNotExist_ReturnsNotFound()
    {
        var request = new UpdateLanguage.Request("Missing", "xx");

        var response = await _client.PutAsJsonAsync($"/api/languages/{Guid.NewGuid()}", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteLanguage_WithExistingId_ThenReturnsNotFound()
    {
        var created = await CreateLanguageAsync("Portuguese", "pt");

        var deleteResponse = await _client.DeleteAsync($"/api/languages/{created.Id}");
        var getResponse = await _client.GetAsync($"/api/languages/{created.Id}");

        Assert.Multiple(() =>
        {
            Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    private async Task<CreateLanguage.Response> CreateLanguageAsync(string name, string code)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/languages",
            new CreateLanguage.Request(name, code));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CreateLanguage.Response>())!;
    }
}
