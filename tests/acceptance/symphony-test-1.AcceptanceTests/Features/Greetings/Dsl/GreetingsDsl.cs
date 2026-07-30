using AcceptanceTests.Core;

namespace AcceptanceTests.Features.Greetings.Dsl;

internal sealed class GreetingsDsl(AcceptanceScenario scenario, ICreateGreetingProtocolDriver createDriver)
{
    private SupportedLanguage? _language;
    private IntroducedGreeting? _greeting;

    public async Task LanguageExistsAsync(string alias, CancellationToken ct)
    {
        _language = await createDriver.CreateLanguageEntryAsync(
            scenario.Data.LanguageName(alias), scenario.Data.LanguageCode(alias), ct);
        scenario.TrackCleanup(token => createDriver.DeleteLanguageAsync(_language, token));
    }

    public async Task CreateGreetingAsync(string text, bool formal, CancellationToken ct)
    {
        await createDriver.CreateGreetingAsync(
            _language ?? throw new AssertionException("A language is required."), text, formal, ct);
        _greeting = new IntroducedGreeting(text, formal);
    }

    public async Task ShouldBeVisibleAsync(CancellationToken ct) =>
        Assert.That(await createDriver.IsVisibleAsync(_language!, _greeting!, ct), Is.True);

    public async Task ShouldGreetingBeVisibleAsync(string text, bool formal, CancellationToken ct) =>
        Assert.That(
            await createDriver.IsVisibleAsync(
                _language ?? throw new AssertionException("A language is required."),
                new IntroducedGreeting(text, formal),
                ct),
            Is.True);
}

internal sealed class GreetingAuthorizationDsl(
    AcceptanceScenario scenario,
    ICreateGreetingProtocolDriver superuserDriver,
    ICreateGreetingAuthorizationProtocolDriver standardUserDriver)
{
    private SupportedLanguage? _language;
    private IntroducedGreeting? _greeting;

    public async Task LanguageExistsAsync(string alias, CancellationToken ct)
    {
        _language = await superuserDriver.CreateLanguageEntryAsync(
            scenario.Data.LanguageName(alias), scenario.Data.LanguageCode(alias), ct);
        scenario.TrackCleanup(token => superuserDriver.DeleteLanguageAsync(_language, token));
    }

    public async Task SuperuserCreatesGreetingAsync(string text, bool formal, CancellationToken ct)
    {
        await superuserDriver.CreateGreetingAsync(
            _language ?? throw new AssertionException("A language is required."), text, formal, ct);
        _greeting = new IntroducedGreeting(text, formal);
    }

    public Task StandardUserCannotCreateGreetingAsync(string text, bool formal, CancellationToken ct) =>
        standardUserDriver.CreateGreetingShouldBeForbiddenAsync(
            _language ?? throw new AssertionException("A language is required."), text, formal, ct);

    public async Task SuperuserCanSeeGreetingAsync(CancellationToken ct) =>
        Assert.That(await superuserDriver.IsVisibleAsync(_language!, _greeting!, ct), Is.True);
}

internal sealed class GreetingDeletionAuthorizationDsl(
    AcceptanceScenario scenario,
    IDeleteGreetingAuthorizationProtocolDriver superuserDriver,
    IDeleteGreetingAuthorizationProtocolDriver standardUserDriver)
{
    private SupportedLanguage? _language;
    private ManagedGreeting? _greeting;

    public async Task LanguageExistsAsync(string alias, CancellationToken ct)
    {
        _language = await superuserDriver.CreateLanguageEntryAsync(
            scenario.Data.LanguageName(alias), scenario.Data.LanguageCode(alias), ct);
        scenario.TrackCleanup(token => superuserDriver.DeleteLanguageAsync(_language, token));
    }

    public async Task SuperuserCreatesGreetingAsync(string text, bool formal, CancellationToken ct) =>
        _greeting = await superuserDriver.CreateGreetingForDeletionAsync(
            _language ?? throw new AssertionException("A language is required."), text, formal, ct);

    public Task SuperuserDeletesGreetingAsync(CancellationToken ct) =>
        superuserDriver.DeleteGreetingAsync(
            _greeting ?? throw new AssertionException("A greeting is required."), ct);

    public Task StandardUserAttemptsToDeleteGreetingAsync(CancellationToken ct) =>
        standardUserDriver.AttemptToDeleteGreetingAsync(
            _greeting ?? throw new AssertionException("A greeting is required."), ct);

    public Task DeletionShouldBeDeniedAsync(CancellationToken ct) =>
        standardUserDriver.DeletionShouldBeDeniedAsync(ct);

    public async Task GreetingShouldBeVisibleAsync(CancellationToken ct) =>
        Assert.That(await superuserDriver.IsGreetingVisibleAsync(
            _language!, _greeting!, ct), Is.True);

    public async Task GreetingShouldNotBeVisibleAsync(CancellationToken ct) =>
        Assert.That(await superuserDriver.IsGreetingVisibleAsync(
            _language!, _greeting!, ct), Is.False);
}

internal sealed class ListGreetingsDsl(AcceptanceScenario scenario, IListGreetingsProtocolDriver listDriver)
{
    private ListedLanguage? _language;
    private IReadOnlyList<ListedGreeting> _greetings = [];

    public async Task SetBusinessTimeAsync(DateTimeOffset utcNow, CancellationToken ct)
    {
        await listDriver.SetBusinessTimeAsync(utcNow, ct);
        scenario.TrackCleanup(listDriver.ResetBusinessTimeAsync);
    }

    public async Task SupportLanguageAsync(string alias, CancellationToken ct)
    {
        _language = await listDriver.SupportLanguageAsync(
            scenario.Data.LanguageName(alias), scenario.Data.LanguageCode(alias), ct);
        scenario.TrackCleanup(token => listDriver.WithdrawLanguageAsync(_language, token));
    }

    public Task IntroduceAsync(string text, CancellationToken ct) =>
        listDriver.IntroduceAsync(_language ?? throw new AssertionException("A language is required."), text, ct);

    public async Task RequestGreetingsIntroducedBetweenAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        _greetings = await listDriver.ListIntroducedBetweenAsync(
            _language ?? throw new AssertionException("A language is required."), from, to, ct);

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

    public Task ShouldBeEmptyAsync()
    {
        Assert.That(_greetings, Is.Empty);
        return Task.CompletedTask;
    }
}

internal sealed record SupportedLanguage(Guid? Id, string Name, string Code);
internal sealed record IntroducedGreeting(string Text, bool Formal);
internal sealed record ManagedGreeting(Guid? Id, string Text, bool Formal);
internal sealed record ListedLanguage(Guid Id);
internal sealed record ListedGreeting(string Text);

internal interface ICreateGreetingProtocolDriver : IAsyncDisposable
{
    Task<SupportedLanguage> CreateLanguageEntryAsync(string name, string code, CancellationToken ct);
    Task CreateGreetingAsync(SupportedLanguage language, string text, bool formal, CancellationToken ct);
    Task<bool> IsVisibleAsync(SupportedLanguage language, IntroducedGreeting greeting, CancellationToken ct);
    Task DeleteLanguageAsync(SupportedLanguage language, CancellationToken ct);
}

internal interface ICreateGreetingAuthorizationProtocolDriver : ICreateGreetingProtocolDriver
{
    Task CreateGreetingShouldBeForbiddenAsync(SupportedLanguage language, string text, bool formal, CancellationToken ct);
}

internal interface IDeleteGreetingAuthorizationProtocolDriver : IAsyncDisposable
{
    Task<SupportedLanguage> CreateLanguageEntryAsync(string name, string code, CancellationToken ct);
    Task<ManagedGreeting> CreateGreetingForDeletionAsync(SupportedLanguage language, string text, bool formal, CancellationToken ct);
    Task DeleteGreetingAsync(ManagedGreeting greeting, CancellationToken ct);
    Task AttemptToDeleteGreetingAsync(ManagedGreeting greeting, CancellationToken ct);
    Task DeletionShouldBeDeniedAsync(CancellationToken ct);
    Task<bool> IsGreetingVisibleAsync(SupportedLanguage language, ManagedGreeting greeting, CancellationToken ct);
    Task DeleteLanguageAsync(SupportedLanguage language, CancellationToken ct);
}

internal interface IListGreetingsProtocolDriver : IAsyncDisposable
{
    Task SetBusinessTimeAsync(DateTimeOffset utcNow, CancellationToken ct);
    Task ResetBusinessTimeAsync(CancellationToken ct);
    Task<ListedLanguage> SupportLanguageAsync(string name, string code, CancellationToken ct);
    Task WithdrawLanguageAsync(ListedLanguage language, CancellationToken ct);
    Task IntroduceAsync(ListedLanguage language, string text, CancellationToken ct);
    Task<IReadOnlyList<ListedGreeting>> ListIntroducedBetweenAsync(
        ListedLanguage language, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
