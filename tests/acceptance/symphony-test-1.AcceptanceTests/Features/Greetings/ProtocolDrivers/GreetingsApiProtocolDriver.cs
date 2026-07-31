using System.Net;
using AcceptanceTests.Core;
using AcceptanceTests.Features.Greetings.Dsl;

namespace AcceptanceTests.Features.Greetings.ProtocolDrivers;

internal sealed class GreetingsApiProtocolDriver(ApiTransport api)
    : ICreateGreetingAuthorizationProtocolDriver,
      IDeleteGreetingAuthorizationProtocolDriver,
      IListGreetingsProtocolDriver,
      IListGreetingsAuthorizationProtocolDriver,
      IGetGreetingAuthorizationProtocolDriver,
      IGetGreetingByLanguageAuthorizationProtocolDriver,
      IUpdateGreetingAuthorizationProtocolDriver,
      ICreateGreetingAuthenticationProtocolDriver,
      IListGreetingsAuthenticationProtocolDriver,
      IGetGreetingAuthenticationProtocolDriver,
      IGetGreetingByLanguageAuthenticationProtocolDriver,
      IUpdateGreetingAuthenticationProtocolDriver,
      IDeleteGreetingAuthenticationProtocolDriver
{
    private static readonly Guid ArbitraryGreetingId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ArbitraryLanguageId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string CreateGreetingDeniedMessage = "You do not have permission to create a greeting.";
    private const string DeleteGreetingDeniedMessage = "You do not have permission to delete this greeting.";
    private const string UpdateGreetingDeniedMessage = "You do not have permission to update this greeting.";
    private ApiResponse? _createGreetingAttempt;
    private ApiResponse? _deleteGreetingAttempt;
    private ApiResponse? _updateGreetingAttempt;
    private ApiResponse? _unauthenticatedCreateAttempt;
    private ApiResponse? _unauthenticatedListAttempt;
    private ApiResponse? _unauthenticatedGetAttempt;
    private ApiResponse? _unauthenticatedGetByLanguageAttempt;
    private ApiResponse? _unauthenticatedUpdateAttempt;
    private ApiResponse? _unauthenticatedDeleteAttempt;
    public async Task<SupportedLanguage> CreateLanguageEntryAsync(string name, string code, CancellationToken ct)
    {
        var result = await api.SendAsync<Language>(
            HttpMethod.Post, "api/languages", new { name, code }, HttpStatusCode.Created, ct);
        return new SupportedLanguage(result!.Id, result.Name, result.Code);
    }

    public Task CreateGreetingAsync(SupportedLanguage language, string text, bool formal, CancellationToken ct) =>
        api.SendAsync<object>(HttpMethod.Post, "api/greetings",
            new { languageId = language.Id, greetingText = text, formal }, HttpStatusCode.Created, ct);

    public async Task AttemptToCreateGreetingAsync(
        SupportedLanguage language,
        string text,
        bool formal,
        CancellationToken ct) =>
        _createGreetingAttempt = await api.SendForResponseAsync(
            HttpMethod.Post,
            "api/greetings",
            new { languageId = language.Id, greetingText = text, formal },
            ct);

    public Task CreationShouldBeDeniedAsync(CancellationToken ct)
    {
        var response = _createGreetingAttempt
            ?? throw new AssertionException("Greeting creation has not been attempted.");
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(response.Body, Does.Contain(CreateGreetingDeniedMessage));
        });
        return Task.CompletedTask;
    }

    public async Task<ManagedGreeting> CreateGreetingForDeletionAsync(
        SupportedLanguage language,
        string text,
        bool formal,
        CancellationToken ct) =>
        await CreateManagedGreetingAsync(language, text, formal, ct);

    public async Task<ManagedGreeting> CreateManagedGreetingAsync(
        SupportedLanguage language,
        string text,
        bool formal,
        CancellationToken ct)
    {
        var greeting = await api.SendAsync<Greeting>(
            HttpMethod.Post,
            "api/greetings",
            new { languageId = language.Id, greetingText = text, formal },
            HttpStatusCode.Created,
            ct);
        return new ManagedGreeting(greeting!.Id, greeting.GreetingText, greeting.Formal);
    }

    public Task DeleteGreetingAsync(ManagedGreeting greeting, CancellationToken ct) =>
        api.SendAsync<object>(HttpMethod.Delete, $"api/greetings/{greeting.Id ?? throw new AssertionException("A greeting identifier is required.")}", null, HttpStatusCode.NoContent, ct);

    public async Task AttemptToDeleteGreetingAsync(ManagedGreeting greeting, CancellationToken ct) =>
        _deleteGreetingAttempt = await api.SendForResponseAsync(
            HttpMethod.Delete,
            $"api/greetings/{greeting.Id ?? throw new AssertionException("A greeting identifier is required.")}",
            null,
            ct);

    public Task DeletionShouldBeDeniedAsync(CancellationToken ct)
    {
        AssertForbidden(_deleteGreetingAttempt, "Greeting deletion", DeleteGreetingDeniedMessage);
        return Task.CompletedTask;
    }

    public async Task<bool> IsGreetingVisibleAsync(
        SupportedLanguage language,
        ManagedGreeting greeting,
        CancellationToken ct)
    {
        var items = await api.SendAsync<List<Greeting>>(
            HttpMethod.Get, $"api/greetings?languageId={language.Id}", null, HttpStatusCode.OK, ct) ?? [];
        return items.Any(item => item.GreetingText == greeting.Text && item.Formal == greeting.Formal);
    }

    public async Task<bool> IsVisibleAsync(SupportedLanguage language, IntroducedGreeting greeting, CancellationToken ct)
    {
        var items = await api.SendAsync<List<Greeting>>(
            HttpMethod.Get, $"api/greetings?languageId={language.Id}", null, HttpStatusCode.OK, ct) ?? [];
        return items.Any(item => item.GreetingText == greeting.Text && item.Formal == greeting.Formal);
    }

    public async Task DeleteLanguageAsync(SupportedLanguage language, CancellationToken ct)
    {
        if (language.Id is Guid id)
        {
            await api.SendAsync<object>(HttpMethod.Delete, $"api/languages/{id}", null, HttpStatusCode.NoContent, ct);
        }
    }

    public Task SetBusinessTimeAsync(DateTimeOffset utcNow, CancellationToken ct) =>
        api.SendAsync<object>(HttpMethod.Put, "api/testing/clock", new { utcNow }, HttpStatusCode.OK, ct);

    public Task ResetBusinessTimeAsync(CancellationToken ct) =>
        api.SendAsync<object>(HttpMethod.Delete, "api/testing/clock", null, HttpStatusCode.NoContent, ct);

    public async Task<ListedLanguage> SupportLanguageAsync(string name, string code, CancellationToken ct)
    {
        var response = await api.SendAsync<Language>(
            HttpMethod.Post, "api/languages", new { name, code }, HttpStatusCode.Created, ct);
        return new ListedLanguage(response!.Id);
    }

    public Task WithdrawLanguageAsync(ListedLanguage language, CancellationToken ct) =>
        api.SendAsync<object>(HttpMethod.Delete, $"api/languages/{language.Id}", null, HttpStatusCode.NoContent, ct);

    public Task IntroduceAsync(ListedLanguage language, string text, CancellationToken ct) =>
        api.SendAsync<object>(HttpMethod.Post, "api/greetings",
            new { languageId = language.Id, greetingText = text, formal = false }, HttpStatusCode.Created, ct);

    public async Task<IReadOnlyList<ListedGreeting>> ListIntroducedBetweenAsync(
        ListedLanguage language, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var query = $"api/greetings?languageId={language.Id}&createdFrom={Uri.EscapeDataString(from.ToString("O"))}&createdTo={Uri.EscapeDataString(to.ToString("O"))}";
        var response = await api.SendAsync<List<Greeting>>(HttpMethod.Get, query, null, HttpStatusCode.OK, ct) ?? [];
        return response.Select(greeting => new ListedGreeting(greeting.GreetingText)).ToList();
    }

    public async Task<IReadOnlyList<ObservedGreeting>> ListGreetingsAsync(CancellationToken ct)
    {
        var response = await api.SendAsync<List<Greeting>>(
            HttpMethod.Get, "api/greetings", null, HttpStatusCode.OK, ct) ?? [];
        return response
            .Select(greeting => new ObservedGreeting(greeting.GreetingText, greeting.Formal))
            .ToList();
    }

    public async Task<ObservedGreeting> GetGreetingAsync(ManagedGreeting greeting, CancellationToken ct)
    {
        var response = await api.SendAsync<Greeting>(
            HttpMethod.Get,
            $"api/greetings/{RequiredGreetingId(greeting)}",
            null,
            HttpStatusCode.OK,
            ct);
        return new ObservedGreeting(response!.GreetingText, response.Formal);
    }

    public async Task<ObservedGreeting> GetGreetingByLanguageAsync(
        SupportedLanguage language,
        bool formal,
        CancellationToken ct)
    {
        var response = await api.SendAsync<GreetingByLanguage>(
            HttpMethod.Get,
            $"api/greetings/by-language/{Uri.EscapeDataString(language.Code)}?formal={formal.ToString().ToLowerInvariant()}",
            null,
            HttpStatusCode.OK,
            ct);
        return new ObservedGreeting(response!.GreetingText, response.Formal);
    }

    public Task UpdateGreetingAsync(
        ManagedGreeting greeting,
        SupportedLanguage language,
        string text,
        bool formal,
        CancellationToken ct) =>
        api.SendAsync<object>(
            HttpMethod.Put,
            $"api/greetings/{RequiredGreetingId(greeting)}",
            new { languageId = language.Id, greetingText = text, formal },
            HttpStatusCode.OK,
            ct);

    public async Task AttemptToUpdateGreetingAsync(
        ManagedGreeting greeting,
        SupportedLanguage language,
        string text,
        bool formal,
        CancellationToken ct) =>
        _updateGreetingAttempt = await api.SendForResponseAsync(
            HttpMethod.Put,
            $"api/greetings/{RequiredGreetingId(greeting)}",
            new { languageId = language.Id, greetingText = text, formal },
            ct);

    public Task UpdateShouldBeDeniedAsync(CancellationToken ct)
    {
        AssertForbidden(_updateGreetingAttempt, "Greeting update", UpdateGreetingDeniedMessage);
        return Task.CompletedTask;
    }

    public Task<ObservedGreeting> GetGreetingStateAsync(ManagedGreeting greeting, CancellationToken ct) =>
        GetGreetingAsync(greeting, ct);

    public async Task AttemptToCreateGreetingWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedCreateAttempt = await api.SendForResponseAsync(
            HttpMethod.Post,
            "api/greetings",
            new
            {
                languageId = ArbitraryLanguageId,
                greetingText = "Anonymous greeting attempt",
                formal = false
            },
            ct);

    public Task AuthenticationShouldBeRequiredAndCreationUnavailableAsync(CancellationToken ct) =>
        AssertUnauthorizedAsync(_unauthenticatedCreateAttempt, "Greeting creation");

    public async Task AttemptToListGreetingsWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedListAttempt = await api.SendForResponseAsync(
            HttpMethod.Get, "api/greetings", null, ct);

    public Task AuthenticationShouldBeRequiredAndListUnavailableAsync(CancellationToken ct) =>
        AssertUnauthorizedAsync(_unauthenticatedListAttempt, "Greeting list");

    public async Task AttemptToGetGreetingWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedGetAttempt = await api.SendForResponseAsync(
            HttpMethod.Get, $"api/greetings/{ArbitraryGreetingId}", null, ct);

    public Task AuthenticationShouldBeRequiredAndDetailsUnavailableAsync(CancellationToken ct) =>
        AssertUnauthorizedAsync(_unauthenticatedGetAttempt, "Greeting details");

    public async Task AttemptToGetGreetingByLanguageWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedGetByLanguageAttempt = await api.SendForResponseAsync(
            HttpMethod.Get, "api/greetings/by-language/anonymous?formal=false", null, ct);

    Task IGetGreetingByLanguageAuthenticationProtocolDriver.AuthenticationShouldBeRequiredAndDetailsUnavailableAsync(
        CancellationToken ct) =>
        AssertUnauthorizedAsync(_unauthenticatedGetByLanguageAttempt, "Greeting lookup by language");

    public async Task AttemptToUpdateGreetingWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedUpdateAttempt = await api.SendForResponseAsync(
            HttpMethod.Put,
            $"api/greetings/{ArbitraryGreetingId}",
            new
            {
                languageId = ArbitraryLanguageId,
                greetingText = "Anonymous greeting update",
                formal = true
            },
            ct);

    public Task AuthenticationShouldBeRequiredAndUpdateUnavailableAsync(CancellationToken ct) =>
        AssertUnauthorizedAsync(_unauthenticatedUpdateAttempt, "Greeting update");

    public async Task AttemptToDeleteGreetingWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedDeleteAttempt = await api.SendForResponseAsync(
            HttpMethod.Delete, $"api/greetings/{ArbitraryGreetingId}", null, ct);

    public Task AuthenticationShouldBeRequiredAndDeletionUnavailableAsync(CancellationToken ct) =>
        AssertUnauthorizedAsync(_unauthenticatedDeleteAttempt, "Greeting deletion");

    public ValueTask DisposeAsync() => api.DisposeAsync();

    private static Guid RequiredGreetingId(ManagedGreeting greeting) =>
        greeting.Id ?? throw new AssertionException("A greeting identifier is required.");

    private static void AssertForbidden(ApiResponse? attempt, string action, string expectedMessage)
    {
        var response = attempt ?? throw new AssertionException($"{action} has not been attempted.");
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(response.Body, Does.Contain(expectedMessage));
        });
    }

    private static Task AssertUnauthorizedAsync(ApiResponse? attempt, string request)
    {
        var response = attempt
            ?? throw new AssertionException($"{request} has not been attempted.");
        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Unauthorized),
            $"{request} must be rejected at the authentication boundary.");
        return Task.CompletedTask;
    }

    private sealed record Language(Guid Id, string Name, string Code);
    private sealed record Greeting(Guid Id, string GreetingText, bool Formal);
    private sealed record GreetingByLanguage(string GreetingText, bool Formal);
}
