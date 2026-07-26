using AcceptanceTests.Core;

namespace AcceptanceTests.Features.Greetings.ListGreetings;

internal sealed class ListGreetingsDsl(AcceptanceScenario scenario, IListGreetingsProtocolDriver driver)
{
    private ListedLanguage? _language;
    private IReadOnlyList<ListedGreeting> _greetings = [];

    public async Task SetBusinessTimeAsync(DateTimeOffset utcNow, CancellationToken ct)
    {
        await driver.SetBusinessTimeAsync(utcNow, ct);
        scenario.TrackCleanup(driver.ResetBusinessTimeAsync);
    }

    public async Task SupportLanguageAsync(string alias, CancellationToken ct)
    {
        _language = await driver.SupportLanguageAsync(scenario.Data.LanguageName(alias), scenario.Data.LanguageCode(alias), ct);
        scenario.TrackCleanup(token => driver.WithdrawLanguageAsync(_language, token));
    }

    public Task IntroduceAsync(string text, CancellationToken ct) =>
        driver.IntroduceAsync(_language ?? throw new AssertionException("A language is required."), text, ct);

    public async Task RequestGreetingsIntroducedBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        _greetings = await driver.ListIntroducedBetweenAsync(_language ?? throw new AssertionException("A language is required."), from, to, ct);

    public Task ShouldIncludeAsync(string text)
    {
        Assert.That(_greetings.Select(greeting => greeting.Text), Does.Contain(text));
        return Task.CompletedTask;
    }

    public Task ShouldNotIncludeAsync(string text)
    {
        Assert.That(_greetings.Select(greeting => greeting.Text), Does.Not.Contain(text));
        return Task.CompletedTask;
    }
}

internal sealed record ListedLanguage(Guid Id);
internal sealed record ListedGreeting(string Text);

internal interface IListGreetingsProtocolDriver : IAsyncDisposable
{
    Task SetBusinessTimeAsync(DateTimeOffset utcNow, CancellationToken ct);
    Task ResetBusinessTimeAsync(CancellationToken ct);
    Task<ListedLanguage> SupportLanguageAsync(string name, string code, CancellationToken ct);
    Task WithdrawLanguageAsync(ListedLanguage language, CancellationToken ct);
    Task IntroduceAsync(ListedLanguage language, string text, CancellationToken ct);
    Task<IReadOnlyList<ListedGreeting>> ListIntroducedBetweenAsync(ListedLanguage language, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
