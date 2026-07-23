using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using SymphonyTest1.Api.Features.Greetings;
using SymphonyTest1.Api.Features.Languages;
using SymphonyTest1.IntegrationTests.Infrastructure;

namespace SymphonyTest1.E2ETests.Features;

[TestFixture]
public class GreetingsE2ETests
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
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task CompleteGreetingWorkflow_CreatesLanguageAndGreeting_CanRetrieveGreeting()
    {
        // Arrange - Create a new language
        var languageRequest = new CreateLanguageRequest("Japanese", "ja");
        var languageResponse = await _client.PostAsJsonAsync("/api/languages", languageRequest);
        var language = await languageResponse.Content.ReadFromJsonAsync<LanguageResponse>();
        Assert.That(language, Is.Not.Null);

        // Act - Create a greeting for that language
        var greetingRequest = new CreateGreetingRequest(language!.Id, "こんにちは", false);
        var greetingResponse = await _client.PostAsJsonAsync("/api/greetings", greetingRequest);
        
        // Assert - Greeting was created
        Assert.That(greetingResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var greeting = await greetingResponse.Content.ReadFromJsonAsync<GreetingResponse>();
        Assert.That(greeting, Is.Not.Null);
        Assert.That(greeting!.GreetingText, Is.EqualTo("こんにちは"));

        // Act - Retrieve greeting by language code
        var getByLanguageResponse = await _client.GetAsync("/api/greetings/by-language/ja");
        
        // Assert - Can retrieve greeting by language
        Assert.That(getByLanguageResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var retrievedGreeting = await getByLanguageResponse.Content.ReadFromJsonAsync<GreetingByLanguageResponse>();
        Assert.That(retrievedGreeting, Is.Not.Null);
        Assert.That(retrievedGreeting!.GreetingText, Is.EqualTo("こんにちは"));
        Assert.That(retrievedGreeting.Language, Is.EqualTo("Japanese"));
    }

    [Test]
    public async Task GetGreetingByLanguage_ForEnglish_ReturnsExpectedGreeting()
    {
        // Act
        var response = await _client.GetAsync("/api/greetings/by-language/en");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var greeting = await response.Content.ReadFromJsonAsync<GreetingByLanguageResponse>();
        Assert.That(greeting, Is.Not.Null);
        Assert.That(greeting!.Language, Is.EqualTo("English"));
        Assert.That(greeting.LanguageCode, Is.EqualTo("en"));
        Assert.That(greeting.GreetingText, Is.Not.Empty);
    }

    [Test]
    public async Task ReferentialIntegrity_DeletingLanguage_CascadesDeleteToGreetings()
    {
        // Arrange - Create language and greeting
        var languageRequest = new CreateLanguageRequest("Chinese", "zh");
        var languageResponse = await _client.PostAsJsonAsync("/api/languages", languageRequest);
        var language = await languageResponse.Content.ReadFromJsonAsync<LanguageResponse>();
        
        var greetingRequest = new CreateGreetingRequest(language!.Id, "你好", false);
        var greetingResponse = await _client.PostAsJsonAsync("/api/greetings", greetingRequest);
        var greeting = await greetingResponse.Content.ReadFromJsonAsync<GreetingResponse>();

        // Act - Delete the language
        var deleteResponse = await _client.DeleteAsync($"/api/languages/{language.Id}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Assert - Greeting should no longer exist
        var getGreetingResponse = await _client.GetAsync($"/api/greetings/{greeting!.Id}");
        Assert.That(getGreetingResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
