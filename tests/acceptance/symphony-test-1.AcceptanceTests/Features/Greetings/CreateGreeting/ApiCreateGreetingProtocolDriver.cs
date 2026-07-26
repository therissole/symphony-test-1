using System.Net;
using AcceptanceTests.Core;

namespace AcceptanceTests.Features.Greetings.CreateGreeting;

internal sealed class ApiCreateGreetingProtocolDriver(ApiTransport api) : ICreateGreetingProtocolDriver
{
    public async Task<SupportedLanguage> CreateLanguageEntryAsync(string name, string code, CancellationToken ct)
    {
        var result = await api.SendAsync<Language>(HttpMethod.Post, "api/languages", new { name, code }, HttpStatusCode.Created, ct);
        return new SupportedLanguage(result!.Id, result.Name, result.Code);
    }
    public async Task CreateGreetingAsync(SupportedLanguage language, string text, bool formal, CancellationToken ct) =>
        await api.SendAsync<object>(HttpMethod.Post, "api/greetings", new { languageId = language.Id, greetingText = text, formal }, HttpStatusCode.Created, ct);
    public async Task<bool> IsVisibleAsync(SupportedLanguage language, IntroducedGreeting greeting, CancellationToken ct)
    {
        var items = await api.SendAsync<List<Greeting>>(HttpMethod.Get, $"api/greetings?languageId={language.Id}", null, HttpStatusCode.OK, ct) ?? [];
        return items.Any(item => item.GreetingText == greeting.Text && item.Formal == greeting.Formal);
    }
    public async Task DeleteLanguageAsync(SupportedLanguage language, CancellationToken ct)
    {
        if (language.Id is Guid id) await api.SendAsync<object>(HttpMethod.Delete, $"api/languages/{id}", null, HttpStatusCode.NoContent, ct);
    }
    public ValueTask DisposeAsync() => api.DisposeAsync();
    private sealed record Language(Guid Id, string Name, string Code);
    private sealed record Greeting(string GreetingText, bool Formal);
}
