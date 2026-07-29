using System.Net;
using AcceptanceTests.Core;
using AcceptanceTests.Features.Greetings.Dsl;

namespace AcceptanceTests.Features.Greetings.ProtocolDrivers;

internal sealed class GreetingsApiProtocolDriver(ApiTransport api)
    : ICreateGreetingProtocolDriver, IListGreetingsProtocolDriver
{
    public async Task<SupportedLanguage> CreateLanguageEntryAsync(string name, string code, CancellationToken ct)
    {
        var result = await api.SendAsync<Language>(
            HttpMethod.Post, "api/languages", new { name, code }, HttpStatusCode.Created, ct);
        return new SupportedLanguage(result!.Id, result.Name, result.Code);
    }

    public Task CreateGreetingAsync(SupportedLanguage language, string text, bool formal, CancellationToken ct) =>
        api.SendAsync<object>(HttpMethod.Post, "api/greetings",
            new { languageId = language.Id, greetingText = text, formal }, HttpStatusCode.Created, ct);

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

    public ValueTask DisposeAsync() => api.DisposeAsync();

    private sealed record Language(Guid Id, string Name, string Code);
    private sealed record Greeting(string GreetingText, bool Formal);
}
