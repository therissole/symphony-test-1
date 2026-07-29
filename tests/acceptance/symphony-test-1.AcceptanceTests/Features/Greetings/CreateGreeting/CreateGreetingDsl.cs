using AcceptanceTests.Core;

namespace AcceptanceTests.Features.Greetings.CreateGreeting;

internal sealed class CreateGreetingDsl(AcceptanceScenario scenario, ICreateGreetingProtocolDriver driver)
{
    private SupportedLanguage? _language;
    private IntroducedGreeting? _greeting;

    public async Task LanguageExistsAsync(string alias, CancellationToken ct)
    {
        // The driver returns a test-owned representation, never an application request or response model.
        _language = await driver.CreateLanguageEntryAsync(scenario.Data.LanguageName(alias), scenario.Data.LanguageCode(alias), ct);
        scenario.TrackCleanup(token => driver.DeleteLanguageAsync(_language, token));
    }

    public async Task CreateGreetingAsync(string text, bool formal, CancellationToken ct)
    {
        await driver.CreateGreetingAsync(_language ?? throw new AssertionException("A language is required."), text, formal, ct);
        _greeting = new IntroducedGreeting(text, formal);
    }

    public async Task ShouldBeVisibleAsync(CancellationToken ct) =>
        Assert.That(await driver.IsVisibleAsync(_language!, _greeting!, ct), Is.True);
}

internal sealed record SupportedLanguage(Guid? Id, string Name, string Code);
internal sealed record IntroducedGreeting(string Text, bool Formal);

internal interface ICreateGreetingProtocolDriver : IAsyncDisposable
{
    Task<SupportedLanguage> CreateLanguageEntryAsync(string name, string code, CancellationToken ct);
    Task CreateGreetingAsync(SupportedLanguage language, string text, bool formal, CancellationToken ct);
    Task<bool> IsVisibleAsync(SupportedLanguage language, IntroducedGreeting greeting, CancellationToken ct);
    Task DeleteLanguageAsync(SupportedLanguage language, CancellationToken ct);
}
