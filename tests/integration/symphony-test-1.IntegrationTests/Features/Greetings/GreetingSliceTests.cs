using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SymphonyTest1.Api.Features.Greetings;
using SymphonyTest1.Api.Features.Languages;
using SymphonyTest1.Api.Infrastructure.Identifiers;
using SymphonyTest1.IntegrationTests.Infrastructure;

namespace SymphonyTest1.IntegrationTests.Features.Greetings;

[TestFixture]
public class GreetingSliceTests
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
    public async Task ListGreetings_ReturnsSeededGreetings()
    {
        var response = await _client.GetAsync("/api/greetings");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var greetings = await response.Content.ReadFromJsonAsync<List<ListGreetings.Response>>();
        Assert.That(greetings, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task ListGreetings_CanFilterByLanguageAndCreationRange()
    {
        var language = await CreateLanguageAsync("Time test language", "tt");
        var start = new DateTimeOffset(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);

        try
        {
            await SetClockAsync(start);
            var oldGreeting = await CreateGreetingAsync(language.Id, "Old greeting");

            await SetClockAsync(start.AddHours(25));
            var newGreeting = await CreateGreetingAsync(language.Id, "New greeting");

            Assert.Multiple(() =>
            {
                Assert.That(oldGreeting.CreatedAt, Is.EqualTo(start));
                Assert.That(newGreeting.CreatedAt, Is.EqualTo(start.AddHours(25)));
            });

            var allGreetings = await _client.GetFromJsonAsync<List<ListGreetings.Response>>("/api/greetings");
            Assert.That(allGreetings?.Select(greeting => greeting.Id), Does.Contain(newGreeting.Id));

            var byLanguageResponse = await _client.GetAsync($"/api/greetings?languageId={language.Id}");
            Assert.That(byLanguageResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var byLanguage = await byLanguageResponse.Content.ReadFromJsonAsync<List<ListGreetings.Response>>();
            Assert.That(byLanguage?.Select(greeting => greeting.Id), Does.Contain(newGreeting.Id));

            var response = await _client.GetAsync(
                $"/api/greetings?languageId={language.Id}&createdFrom={Uri.EscapeDataString(start.AddHours(1).ToString("O"))}&createdTo={Uri.EscapeDataString(start.AddHours(25).AddMicroseconds(1).ToString("O"))}");
            var greetings = await response.Content.ReadFromJsonAsync<List<ListGreetings.Response>>();

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(greetings?.Select(greeting => greeting.Id), Does.Contain(newGreeting.Id));
                Assert.That(greetings?.Select(greeting => greeting.Id), Does.Not.Contain(oldGreeting.Id));
            });
        }
        finally
        {
            await _client.DeleteAsync("/api/testing/clock");
        }
    }

    [Test]
    public async Task GetGreeting_WhenGreetingDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/greetings/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetGreetingByLanguage_WithFormalFilter_ReturnsMatchingGreeting()
    {
        var response = await _client.GetAsync("/api/greetings/by-language/en?formal=true");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var greeting = await response.Content.ReadFromJsonAsync<GetGreetingByLanguage.Response>();
        Assert.Multiple(() =>
        {
            Assert.That(greeting?.LanguageCode, Is.EqualTo("en"));
            Assert.That(greeting?.Formal, Is.True);
        });
    }

    [Test]
    public async Task CreateAndUpdateGreeting_WithValidRequests_ReturnsCurrentRepresentation()
    {
        var language = await CreateLanguageAsync("Dutch", "nl");
        var createRequest = new CreateGreeting.Request(language.Id, "Hallo", false);

        var createResponse = await _client.PostAsJsonAsync("/api/greetings", createRequest);
        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateGreeting.Response>();
        Assert.That(created?.GreetingText, Is.EqualTo("Hallo"));

        var updateRequest = new UpdateGreeting.Request(language.Id, "Goedendag", true);
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/greetings/{created!.Id}",
            updateRequest);

        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await updateResponse.Content.ReadFromJsonAsync<UpdateGreeting.Response>();
        Assert.Multiple(() =>
        {
            Assert.That(updated?.GreetingText, Is.EqualTo("Goedendag"));
            Assert.That(updated?.Formal, Is.True);
        });
    }

    [Test]
    public async Task CreateGreeting_WithMissingLanguage_ReturnsValidationProblem()
    {
        var request = new CreateGreeting.Request(
            new LanguageId(Guid.NewGuid()),
            "Hello",
            false);

        var response = await _client.PostAsJsonAsync("/api/greetings", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.That(problem?.Errors, Does.ContainKey("languageId"));
    }

    [Test]
    public async Task CreateGreeting_WithInvalidRequest_ReturnsValidationProblem()
    {
        var request = new CreateGreeting.Request(default, "", false);

        var response = await _client.PostAsJsonAsync("/api/greetings", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Errors, Does.ContainKey("languageId"));
            Assert.That(problem.Errors, Does.ContainKey("greetingText"));
        });
    }

    [Test]
    public async Task DeleteGreeting_WithExistingId_ThenReturnsNotFound()
    {
        var language = await CreateLanguageAsync("Swedish", "sv");
        var greeting = await CreateGreetingAsync(language.Id, "Hej");

        var deleteResponse = await _client.DeleteAsync($"/api/greetings/{greeting.Id}");
        var getResponse = await _client.GetAsync($"/api/greetings/{greeting.Id}");

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

    private async Task<CreateGreeting.Response> CreateGreetingAsync(
        LanguageId languageId,
        string text)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/greetings",
            new CreateGreeting.Request(languageId, text, false));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CreateGreeting.Response>())!;
    }

    private async Task SetClockAsync(DateTimeOffset utcNow)
    {
        var response = await _client.PutAsJsonAsync("/api/testing/clock", new { utcNow });
        response.EnsureSuccessStatusCode();
    }
}
