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

internal sealed class GreetingCreationAuthorizationDsl(
    AcceptanceScenario scenario,
    ICreateGreetingProtocolDriver superuserDriver,
    ICreateGreetingAuthorizationProtocolDriver standardUserDriver)
{
    private SupportedLanguage? _language;
    private IntroducedGreeting? _greeting;
    private IntroducedGreeting? _attemptedGreeting;

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

    public async Task StandardUserAttemptsToCreateGreetingAsync(string text, bool formal, CancellationToken ct)
    {
        _attemptedGreeting = new IntroducedGreeting(text, formal);
        await standardUserDriver.AttemptToCreateGreetingAsync(
            _language ?? throw new AssertionException("A language is required."), text, formal, ct);
    }

    public Task CreationShouldBeDeniedAsync(CancellationToken ct) =>
        standardUserDriver.CreationShouldBeDeniedAsync(ct);

    public async Task AttemptedGreetingShouldNotBeVisibleAsync(CancellationToken ct) =>
        Assert.That(
            await superuserDriver.IsVisibleAsync(
                _language ?? throw new AssertionException("A language is required."),
                _attemptedGreeting ?? throw new AssertionException("A greeting creation must be attempted."),
                ct),
            Is.False);

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

internal sealed class ListGreetingsAuthorizationDsl(
    AcceptanceScenario scenario,
    IListGreetingsAuthorizationProtocolDriver superuserDriver,
    IListGreetingsAuthorizationProtocolDriver standardUserDriver)
{
    private SupportedLanguage? _language;
    private ManagedGreeting? _greeting;
    private IReadOnlyList<ObservedGreeting> _observedGreetings = [];

    public async Task GreetingExistsAsync(string languageAlias, string text, bool formal, CancellationToken ct)
    {
        _language = await superuserDriver.CreateLanguageEntryAsync(
            scenario.Data.LanguageName(languageAlias), scenario.Data.LanguageCode(languageAlias), ct);
        scenario.TrackCleanup(token => superuserDriver.DeleteLanguageAsync(_language, token));
        _greeting = await superuserDriver.CreateManagedGreetingAsync(_language, text, formal, ct);
    }

    public async Task SuperuserListsGreetingsAsync(CancellationToken ct) =>
        _observedGreetings = await superuserDriver.ListGreetingsAsync(ct);

    public async Task StandardUserListsGreetingsAsync(CancellationToken ct) =>
        _observedGreetings = await standardUserDriver.ListGreetingsAsync(ct);

    public Task GreetingShouldBeListedAsync()
    {
        var greeting = _greeting ?? throw new AssertionException("A greeting is required.");
        Assert.That(
            _observedGreetings.Any(item =>
                item.Text == greeting.Text && item.Formal == greeting.Formal),
            Is.True);
        return Task.CompletedTask;
    }
}

internal sealed class GetGreetingAuthorizationDsl(
    AcceptanceScenario scenario,
    IGetGreetingAuthorizationProtocolDriver superuserDriver,
    IGetGreetingAuthorizationProtocolDriver standardUserDriver)
{
    private SupportedLanguage? _language;
    private ManagedGreeting? _greeting;
    private ObservedGreeting? _observedGreeting;

    public async Task GreetingExistsAsync(string languageAlias, string text, bool formal, CancellationToken ct)
    {
        _language = await superuserDriver.CreateLanguageEntryAsync(
            scenario.Data.LanguageName(languageAlias), scenario.Data.LanguageCode(languageAlias), ct);
        scenario.TrackCleanup(token => superuserDriver.DeleteLanguageAsync(_language, token));
        _greeting = await superuserDriver.CreateManagedGreetingAsync(_language, text, formal, ct);
    }

    public async Task SuperuserGetsGreetingAsync(CancellationToken ct) =>
        _observedGreeting = await superuserDriver.GetGreetingAsync(
            _greeting ?? throw new AssertionException("A greeting is required."), ct);

    public async Task StandardUserGetsGreetingAsync(CancellationToken ct) =>
        _observedGreeting = await standardUserDriver.GetGreetingAsync(
            _greeting ?? throw new AssertionException("A greeting is required."), ct);

    public Task GreetingDetailsShouldBeVisibleAsync()
    {
        var greeting = _greeting ?? throw new AssertionException("A greeting is required.");
        Assert.That(_observedGreeting, Is.EqualTo(new ObservedGreeting(greeting.Text, greeting.Formal)));
        return Task.CompletedTask;
    }
}

internal sealed class GetGreetingByLanguageAuthorizationDsl(
    AcceptanceScenario scenario,
    IGetGreetingByLanguageAuthorizationProtocolDriver superuserDriver,
    IGetGreetingByLanguageAuthorizationProtocolDriver standardUserDriver)
{
    private SupportedLanguage? _language;
    private ManagedGreeting? _greeting;
    private ObservedGreeting? _observedGreeting;

    public async Task GreetingExistsAsync(string languageAlias, string text, bool formal, CancellationToken ct)
    {
        _language = await superuserDriver.CreateLanguageEntryAsync(
            scenario.Data.LanguageName(languageAlias), scenario.Data.LanguageCode(languageAlias), ct);
        scenario.TrackCleanup(token => superuserDriver.DeleteLanguageAsync(_language, token));
        _greeting = await superuserDriver.CreateManagedGreetingAsync(_language, text, formal, ct);
    }

    public async Task SuperuserGetsGreetingByLanguageAsync(CancellationToken ct) =>
        _observedGreeting = await superuserDriver.GetGreetingByLanguageAsync(
            _language ?? throw new AssertionException("A language is required."),
            RequiredGreeting.Formal,
            ct);

    public async Task StandardUserGetsGreetingByLanguageAsync(CancellationToken ct) =>
        _observedGreeting = await standardUserDriver.GetGreetingByLanguageAsync(
            _language ?? throw new AssertionException("A language is required."),
            RequiredGreeting.Formal,
            ct);

    public Task GreetingDetailsShouldBeVisibleAsync()
    {
        var greeting = _greeting ?? throw new AssertionException("A greeting is required.");
        Assert.That(_observedGreeting, Is.EqualTo(new ObservedGreeting(greeting.Text, greeting.Formal)));
        return Task.CompletedTask;
    }

    private ManagedGreeting RequiredGreeting =>
        _greeting ?? throw new AssertionException("A greeting is required.");
}

internal sealed class UpdateGreetingAuthorizationDsl(
    AcceptanceScenario scenario,
    IUpdateGreetingAuthorizationProtocolDriver superuserDriver,
    IUpdateGreetingAuthorizationProtocolDriver standardUserDriver)
{
    private SupportedLanguage? _language;
    private ManagedGreeting? _greeting;
    private IntroducedGreeting? _requestedUpdate;

    public async Task GreetingExistsAsync(string languageAlias, string text, bool formal, CancellationToken ct)
    {
        _language = await superuserDriver.CreateLanguageEntryAsync(
            scenario.Data.LanguageName(languageAlias), scenario.Data.LanguageCode(languageAlias), ct);
        scenario.TrackCleanup(token => superuserDriver.DeleteLanguageAsync(_language, token));
        _greeting = await superuserDriver.CreateManagedGreetingAsync(_language, text, formal, ct);
    }

    public async Task SuperuserUpdatesGreetingAsync(string text, bool formal, CancellationToken ct)
    {
        _requestedUpdate = new IntroducedGreeting(text, formal);
        await superuserDriver.UpdateGreetingAsync(
            _greeting ?? throw new AssertionException("A greeting is required."),
            _language ?? throw new AssertionException("A language is required."),
            text,
            formal,
            ct);
    }

    public async Task StandardUserAttemptsToUpdateGreetingAsync(string text, bool formal, CancellationToken ct)
    {
        _requestedUpdate = new IntroducedGreeting(text, formal);
        await standardUserDriver.AttemptToUpdateGreetingAsync(
            _greeting ?? throw new AssertionException("A greeting is required."),
            _language ?? throw new AssertionException("A language is required."),
            text,
            formal,
            ct);
    }

    public Task UpdateShouldBeDeniedAsync(CancellationToken ct) =>
        standardUserDriver.UpdateShouldBeDeniedAsync(ct);

    public async Task GreetingShouldContainRequestedUpdateAsync(CancellationToken ct)
    {
        var updated = await superuserDriver.GetGreetingStateAsync(
            _greeting ?? throw new AssertionException("A greeting is required."), ct);
        var requested = _requestedUpdate ?? throw new AssertionException("A greeting update is required.");
        Assert.That(updated, Is.EqualTo(new ObservedGreeting(requested.Text, requested.Formal)));
    }

    public async Task GreetingShouldRemainUnchangedAsync(CancellationToken ct)
    {
        var greeting = _greeting ?? throw new AssertionException("A greeting is required.");
        var persisted = await superuserDriver.GetGreetingStateAsync(greeting, ct);
        Assert.That(persisted, Is.EqualTo(new ObservedGreeting(greeting.Text, greeting.Formal)));
    }
}

internal sealed class CreateGreetingAuthenticationDsl(
    ICreateGreetingAuthenticationProtocolDriver unauthenticatedDriver)
{
    public Task UnauthenticatedPersonAttemptsToCreateGreetingAsync(CancellationToken ct) =>
        unauthenticatedDriver.AttemptToCreateGreetingWithoutAuthenticationAsync(ct);

    public Task AuthenticationShouldBeRequiredAndCreationUnavailableAsync(CancellationToken ct) =>
        unauthenticatedDriver.AuthenticationShouldBeRequiredAndCreationUnavailableAsync(ct);
}

internal sealed class ListGreetingsAuthenticationDsl(
    IListGreetingsAuthenticationProtocolDriver unauthenticatedDriver)
{
    public Task UnauthenticatedPersonAttemptsToListGreetingsAsync(CancellationToken ct) =>
        unauthenticatedDriver.AttemptToListGreetingsWithoutAuthenticationAsync(ct);

    public Task AuthenticationShouldBeRequiredAndListUnavailableAsync(CancellationToken ct) =>
        unauthenticatedDriver.AuthenticationShouldBeRequiredAndListUnavailableAsync(ct);
}

internal sealed class GetGreetingAuthenticationDsl(
    IGetGreetingAuthenticationProtocolDriver unauthenticatedDriver)
{
    public Task UnauthenticatedPersonAttemptsToViewGreetingAsync(CancellationToken ct) =>
        unauthenticatedDriver.AttemptToGetGreetingWithoutAuthenticationAsync(ct);

    public Task AuthenticationShouldBeRequiredAndDetailsUnavailableAsync(CancellationToken ct) =>
        unauthenticatedDriver.AuthenticationShouldBeRequiredAndDetailsUnavailableAsync(ct);
}

internal sealed class GetGreetingByLanguageAuthenticationDsl(
    IGetGreetingByLanguageAuthenticationProtocolDriver unauthenticatedDriver)
{
    public Task UnauthenticatedPersonAttemptsToFindGreetingByLanguageAsync(CancellationToken ct) =>
        unauthenticatedDriver.AttemptToGetGreetingByLanguageWithoutAuthenticationAsync(ct);

    public Task AuthenticationShouldBeRequiredAndDetailsUnavailableAsync(CancellationToken ct) =>
        unauthenticatedDriver.AuthenticationShouldBeRequiredAndDetailsUnavailableAsync(ct);
}

internal sealed class UpdateGreetingAuthenticationDsl(
    IUpdateGreetingAuthenticationProtocolDriver unauthenticatedDriver)
{
    public Task UnauthenticatedPersonAttemptsToUpdateGreetingAsync(CancellationToken ct) =>
        unauthenticatedDriver.AttemptToUpdateGreetingWithoutAuthenticationAsync(ct);

    public Task AuthenticationShouldBeRequiredAndUpdateUnavailableAsync(CancellationToken ct) =>
        unauthenticatedDriver.AuthenticationShouldBeRequiredAndUpdateUnavailableAsync(ct);
}

internal sealed class DeleteGreetingAuthenticationDsl(
    IDeleteGreetingAuthenticationProtocolDriver unauthenticatedDriver)
{
    public Task UnauthenticatedPersonAttemptsToDeleteGreetingAsync(CancellationToken ct) =>
        unauthenticatedDriver.AttemptToDeleteGreetingWithoutAuthenticationAsync(ct);

    public Task AuthenticationShouldBeRequiredAndDeletionUnavailableAsync(CancellationToken ct) =>
        unauthenticatedDriver.AuthenticationShouldBeRequiredAndDeletionUnavailableAsync(ct);
}

internal sealed record SupportedLanguage(Guid? Id, string Name, string Code);
internal sealed record IntroducedGreeting(string Text, bool Formal);
internal sealed record ManagedGreeting(Guid? Id, string Text, bool Formal);
internal sealed record ListedLanguage(Guid Id);
internal sealed record ListedGreeting(string Text);
internal sealed record ObservedGreeting(string Text, bool Formal);

internal interface ICreateGreetingProtocolDriver : IAsyncDisposable
{
    Task<SupportedLanguage> CreateLanguageEntryAsync(string name, string code, CancellationToken ct);
    Task CreateGreetingAsync(SupportedLanguage language, string text, bool formal, CancellationToken ct);
    Task<bool> IsVisibleAsync(SupportedLanguage language, IntroducedGreeting greeting, CancellationToken ct);
    Task DeleteLanguageAsync(SupportedLanguage language, CancellationToken ct);
}

internal interface ICreateGreetingAuthorizationProtocolDriver : ICreateGreetingProtocolDriver
{
    Task AttemptToCreateGreetingAsync(SupportedLanguage language, string text, bool formal, CancellationToken ct);
    Task CreationShouldBeDeniedAsync(CancellationToken ct);
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

internal interface IListGreetingsAuthorizationProtocolDriver : IAsyncDisposable
{
    Task<SupportedLanguage> CreateLanguageEntryAsync(string name, string code, CancellationToken ct);
    Task<ManagedGreeting> CreateManagedGreetingAsync(
        SupportedLanguage language, string text, bool formal, CancellationToken ct);
    Task<IReadOnlyList<ObservedGreeting>> ListGreetingsAsync(CancellationToken ct);
    Task DeleteLanguageAsync(SupportedLanguage language, CancellationToken ct);
}

internal interface IGetGreetingAuthorizationProtocolDriver : IAsyncDisposable
{
    Task<SupportedLanguage> CreateLanguageEntryAsync(string name, string code, CancellationToken ct);
    Task<ManagedGreeting> CreateManagedGreetingAsync(
        SupportedLanguage language, string text, bool formal, CancellationToken ct);
    Task<ObservedGreeting> GetGreetingAsync(ManagedGreeting greeting, CancellationToken ct);
    Task DeleteLanguageAsync(SupportedLanguage language, CancellationToken ct);
}

internal interface IGetGreetingByLanguageAuthorizationProtocolDriver : IAsyncDisposable
{
    Task<SupportedLanguage> CreateLanguageEntryAsync(string name, string code, CancellationToken ct);
    Task<ManagedGreeting> CreateManagedGreetingAsync(
        SupportedLanguage language, string text, bool formal, CancellationToken ct);
    Task<ObservedGreeting> GetGreetingByLanguageAsync(
        SupportedLanguage language, bool formal, CancellationToken ct);
    Task DeleteLanguageAsync(SupportedLanguage language, CancellationToken ct);
}

internal interface IUpdateGreetingAuthorizationProtocolDriver : IAsyncDisposable
{
    Task<SupportedLanguage> CreateLanguageEntryAsync(string name, string code, CancellationToken ct);
    Task<ManagedGreeting> CreateManagedGreetingAsync(
        SupportedLanguage language, string text, bool formal, CancellationToken ct);
    Task UpdateGreetingAsync(
        ManagedGreeting greeting,
        SupportedLanguage language,
        string text,
        bool formal,
        CancellationToken ct);
    Task AttemptToUpdateGreetingAsync(
        ManagedGreeting greeting,
        SupportedLanguage language,
        string text,
        bool formal,
        CancellationToken ct);
    Task UpdateShouldBeDeniedAsync(CancellationToken ct);
    Task<ObservedGreeting> GetGreetingStateAsync(ManagedGreeting greeting, CancellationToken ct);
    Task DeleteLanguageAsync(SupportedLanguage language, CancellationToken ct);
}

internal interface ICreateGreetingAuthenticationProtocolDriver : IAsyncDisposable
{
    Task AttemptToCreateGreetingWithoutAuthenticationAsync(CancellationToken ct);
    Task AuthenticationShouldBeRequiredAndCreationUnavailableAsync(CancellationToken ct);
}

internal interface IListGreetingsAuthenticationProtocolDriver : IAsyncDisposable
{
    Task AttemptToListGreetingsWithoutAuthenticationAsync(CancellationToken ct);
    Task AuthenticationShouldBeRequiredAndListUnavailableAsync(CancellationToken ct);
}

internal interface IGetGreetingAuthenticationProtocolDriver : IAsyncDisposable
{
    Task AttemptToGetGreetingWithoutAuthenticationAsync(CancellationToken ct);
    Task AuthenticationShouldBeRequiredAndDetailsUnavailableAsync(CancellationToken ct);
}

internal interface IGetGreetingByLanguageAuthenticationProtocolDriver : IAsyncDisposable
{
    Task AttemptToGetGreetingByLanguageWithoutAuthenticationAsync(CancellationToken ct);
    Task AuthenticationShouldBeRequiredAndDetailsUnavailableAsync(CancellationToken ct);
}

internal interface IUpdateGreetingAuthenticationProtocolDriver : IAsyncDisposable
{
    Task AttemptToUpdateGreetingWithoutAuthenticationAsync(CancellationToken ct);
    Task AuthenticationShouldBeRequiredAndUpdateUnavailableAsync(CancellationToken ct);
}

internal interface IDeleteGreetingAuthenticationProtocolDriver : IAsyncDisposable
{
    Task AttemptToDeleteGreetingWithoutAuthenticationAsync(CancellationToken ct);
    Task AuthenticationShouldBeRequiredAndDeletionUnavailableAsync(CancellationToken ct);
}
