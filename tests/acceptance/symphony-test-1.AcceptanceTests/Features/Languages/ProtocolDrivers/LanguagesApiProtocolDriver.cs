using System.Net;
using AcceptanceTests.Core;
using AcceptanceTests.Features.Languages.Dsl;

namespace AcceptanceTests.Features.Languages.ProtocolDrivers;

internal sealed class LanguagesApiProtocolDriver(ApiTransport api)
    : ICreateLanguageProtocolDriver,
      IListLanguagesProtocolDriver,
      IGetLanguageProtocolDriver,
      IUpdateLanguageProtocolDriver,
      IDeleteLanguageProtocolDriver
{
    private const string CreateDeniedMessage = "You do not have permission to create a language.";
    private const string UpdateDeniedMessage = "You do not have permission to update this language.";
    private const string DeleteDeniedMessage = "You do not have permission to delete this language.";
    private ApiResponse? _creationAttempt;
    private ApiResponse? _updateAttempt;
    private ApiResponse? _deletionAttempt;
    private ApiResponse? _unauthenticatedAttempt;

    public async Task<ManagedLanguage> CreateLanguageAsync(string name, string code, CancellationToken ct)
    {
        var language = await api.SendAsync<Language>(
            HttpMethod.Post, "api/languages", new { name, code }, HttpStatusCode.Created, ct);
        return new ManagedLanguage(language!.Id, language.Name, language.Code);
    }

    public async Task AttemptToCreateLanguageAsync(ManagedLanguage language, CancellationToken ct) =>
        _creationAttempt = await api.SendForResponseAsync(
            HttpMethod.Post, "api/languages", new { language.Name, language.Code }, ct);

    public Task CreationShouldBeDeniedAsync(CancellationToken ct) =>
        AssertDeniedAsync(_creationAttempt, CreateDeniedMessage, "Language creation");

    public async Task AttemptToCreateLanguageWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedAttempt = await api.SendForResponseAsync(
            HttpMethod.Post,
            "api/languages",
            new { name = "Unauthenticated language", code = "UA" },
            ct);

    public Task CreationShouldRequireAuthenticationAsync(CancellationToken ct) =>
        AssertAuthenticationRequiredAsync(_unauthenticatedAttempt, "Language creation");

    public async Task<bool> IsLanguageVisibleAsync(ManagedLanguage language, CancellationToken ct) =>
        (await ListLanguagesAsync(ct)).Any(item =>
            item.Name == language.Name && item.Code == language.Code);

    public Task<bool> IsLanguageListedAsync(ManagedLanguage language, CancellationToken ct) =>
        IsLanguageVisibleAsync(language, ct);

    public async Task AttemptToListLanguagesWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedAttempt = await api.SendForResponseAsync(
            HttpMethod.Get, "api/languages", null, ct);

    public Task ListingShouldRequireAuthenticationAsync(CancellationToken ct) =>
        AssertAuthenticationRequiredAsync(_unauthenticatedAttempt, "Language listing");

    public async Task<bool> CanViewLanguageDetailsAsync(ManagedLanguage language, CancellationToken ct)
    {
        var response = await api.SendAsync<Language>(
            HttpMethod.Get, $"api/languages/{RequiredId(language)}", null, HttpStatusCode.OK, ct);
        return response?.Name == language.Name && response.Code == language.Code;
    }

    public async Task AttemptToViewLanguageWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedAttempt = await api.SendForResponseAsync(
            HttpMethod.Get, $"api/languages/{Guid.NewGuid()}", null, ct);

    public Task ViewingShouldRequireAuthenticationAsync(CancellationToken ct) =>
        AssertAuthenticationRequiredAsync(_unauthenticatedAttempt, "Viewing a language");

    public Task UpdateLanguageAsync(
        ManagedLanguage language,
        ManagedLanguage update,
        CancellationToken ct) =>
        api.SendAsync<Language>(
            HttpMethod.Put,
            $"api/languages/{RequiredId(language)}",
            new { update.Name, update.Code },
            HttpStatusCode.OK,
            ct);

    public async Task AttemptToUpdateLanguageAsync(
        ManagedLanguage language,
        ManagedLanguage update,
        CancellationToken ct) =>
        _updateAttempt = await api.SendForResponseAsync(
            HttpMethod.Put,
            $"api/languages/{RequiredId(language)}",
            new { update.Name, update.Code },
            ct);

    public Task UpdateShouldBeDeniedAsync(CancellationToken ct) =>
        AssertDeniedAsync(_updateAttempt, UpdateDeniedMessage, "Language update");

    public async Task AttemptToUpdateLanguageWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedAttempt = await api.SendForResponseAsync(
            HttpMethod.Put,
            $"api/languages/{Guid.NewGuid()}",
            new { name = "Unauthenticated update", code = "UU" },
            ct);

    public Task UpdateShouldRequireAuthenticationAsync(CancellationToken ct) =>
        AssertAuthenticationRequiredAsync(_unauthenticatedAttempt, "Language update");

    public async Task<bool> LanguageMatchesAsync(
        ManagedLanguage language,
        ManagedLanguage expected,
        CancellationToken ct)
    {
        var response = await api.SendAsync<Language>(
            HttpMethod.Get, $"api/languages/{RequiredId(language)}", null, HttpStatusCode.OK, ct);
        return response?.Name == expected.Name && response.Code == expected.Code;
    }

    public Task DeleteLanguageAsync(ManagedLanguage language, CancellationToken ct) =>
        api.SendAsync<object>(
            HttpMethod.Delete,
            $"api/languages/{RequiredId(language)}",
            null,
            HttpStatusCode.NoContent,
            ct);

    public async Task CleanupLanguageAsync(ManagedLanguage language, CancellationToken ct)
    {
        if (!await IsLanguageVisibleAsync(language, ct))
        {
            return;
        }

        await DeleteLanguageAsync(language, ct);
    }

    public async Task AttemptToDeleteLanguageAsync(ManagedLanguage language, CancellationToken ct) =>
        _deletionAttempt = await api.SendForResponseAsync(
            HttpMethod.Delete, $"api/languages/{RequiredId(language)}", null, ct);

    public Task DeletionShouldBeDeniedAsync(CancellationToken ct) =>
        AssertDeniedAsync(_deletionAttempt, DeleteDeniedMessage, "Language deletion");

    public async Task AttemptToDeleteLanguageWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedAttempt = await api.SendForResponseAsync(
            HttpMethod.Delete, $"api/languages/{Guid.NewGuid()}", null, ct);

    public Task DeletionShouldRequireAuthenticationAsync(CancellationToken ct) =>
        AssertAuthenticationRequiredAsync(_unauthenticatedAttempt, "Language deletion");

    public ValueTask DisposeAsync() => api.DisposeAsync();

    private async Task<IReadOnlyList<Language>> ListLanguagesAsync(CancellationToken ct) =>
        await api.SendAsync<List<Language>>(
            HttpMethod.Get, "api/languages", null, HttpStatusCode.OK, ct) ?? [];

    private static Task AssertDeniedAsync(ApiResponse? response, string message, string action)
    {
        var deniedResponse = response ?? throw new AssertionException($"{action} has not been attempted.");
        Assert.Multiple(() =>
        {
            Assert.That(deniedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(deniedResponse.Body, Does.Contain(message));
        });
        return Task.CompletedTask;
    }

    private static Task AssertAuthenticationRequiredAsync(ApiResponse? response, string action)
    {
        var rejectedResponse = response ??
            throw new AssertionException($"{action} has not been attempted without authentication.");
        Assert.That(rejectedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        return Task.CompletedTask;
    }

    private static Guid RequiredId(ManagedLanguage language) =>
        language.Id ?? throw new AssertionException("A language identifier is required.");

    private sealed record Language(Guid Id, string Name, string Code);
}
