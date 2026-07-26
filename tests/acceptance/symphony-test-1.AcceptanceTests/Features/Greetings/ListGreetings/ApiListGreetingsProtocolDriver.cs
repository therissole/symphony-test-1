using System.Net;
using AcceptanceTests.Core;

namespace AcceptanceTests.Features.Greetings.ListGreetings;

internal sealed class ApiListGreetingsProtocolDriver(ApiTransport api) : IListGreetingsProtocolDriver
{
    public async Task SetBusinessTimeAsync(DateTimeOffset utcNow, CancellationToken ct) =>
        await api.SendAsync<object>(HttpMethod.Put, "api/testing/clock", new { utcNow }, HttpStatusCode.OK, ct);

    public async Task ResetBusinessTimeAsync(CancellationToken ct) =>
        await api.SendAsync<object>(HttpMethod.Delete, "api/testing/clock", null, HttpStatusCode.NoContent, ct);

    public async Task<ListedLanguage> SupportLanguageAsync(string name, string code, CancellationToken ct)
    {
        var response = await api.SendAsync<Language>(HttpMethod.Post, "api/languages", new { name, code }, HttpStatusCode.Created, ct);
        return new ListedLanguage(response!.Id);
    }

    public async Task WithdrawLanguageAsync(ListedLanguage language, CancellationToken ct) =>
        await api.SendAsync<object>(HttpMethod.Delete, $"api/languages/{language.Id}", null, HttpStatusCode.NoContent, ct);

    public async Task IntroduceAsync(ListedLanguage language, string text, CancellationToken ct) =>
        await api.SendAsync<object>(HttpMethod.Post, "api/greetings", new { languageId = language.Id, greetingText = text, formal = false }, HttpStatusCode.Created, ct);

    public async Task<IReadOnlyList<ListedGreeting>> ListIntroducedBetweenAsync(ListedLanguage language, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var query = $"api/greetings?languageId={language.Id}&createdFrom={Uri.EscapeDataString(from.ToString("O"))}&createdTo={Uri.EscapeDataString(to.ToString("O"))}";
        var response = await api.SendAsync<List<Greeting>>(HttpMethod.Get, query, null, HttpStatusCode.OK, ct) ?? [];
        return response.Select(greeting => new ListedGreeting(greeting.GreetingText)).ToList();
    }

    public ValueTask DisposeAsync() => api.DisposeAsync();

    private sealed record Language(Guid Id);
    private sealed record Greeting(string GreetingText);
}
