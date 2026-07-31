using AcceptanceTests.Core;

namespace AcceptanceTests.Features.Languages.Dsl;

internal sealed class CreateLanguageDsl(
    AcceptanceScenario scenario,
    ICreateLanguageProtocolDriver superuserDriver,
    ICreateLanguageProtocolDriver standardUserDriver,
    ICreateLanguageProtocolDriver anonymousDriver)
{
    private ManagedLanguage? _language;

    public async Task SuperuserCreatesLanguageAsync(string alias, CancellationToken ct)
    {
        _language = await superuserDriver.CreateLanguageAsync(
            scenario.Data.LanguageName(alias), scenario.Data.LanguageCode(alias), ct);
        scenario.TrackCleanup(token => superuserDriver.DeleteLanguageAsync(_language, token));
    }

    public async Task StandardUserAttemptsToCreateLanguageAsync(string alias, CancellationToken ct)
    {
        _language = new ManagedLanguage(
            null, scenario.Data.LanguageName(alias), scenario.Data.LanguageCode(alias));
        await standardUserDriver.AttemptToCreateLanguageAsync(_language, ct);
    }

    public Task StandardUserAttemptsToCreateInvalidLanguageAsync(CancellationToken ct) =>
        standardUserDriver.AttemptToCreateLanguageAsync(new ManagedLanguage(null, string.Empty, string.Empty), ct);

    public Task UnauthenticatedPersonAttemptsToCreateLanguageAsync(CancellationToken ct) =>
        anonymousDriver.AttemptToCreateLanguageWithoutAuthenticationAsync(ct);

    public Task CreationShouldBeDeniedAsync(CancellationToken ct) =>
        standardUserDriver.CreationShouldBeDeniedAsync(ct);

    public Task AuthenticationShouldBeRequiredAsync(CancellationToken ct) =>
        anonymousDriver.CreationShouldRequireAuthenticationAsync(ct);

    public async Task LanguageShouldBeVisibleAsync(CancellationToken ct) =>
        Assert.That(
            await superuserDriver.IsLanguageVisibleAsync(
                _language ?? throw new AssertionException("A language is required."), ct),
            Is.True);

    public async Task LanguageShouldNotBeVisibleAsync(CancellationToken ct) =>
        Assert.That(
            await superuserDriver.IsLanguageVisibleAsync(
                _language ?? throw new AssertionException("A language is required."), ct),
            Is.False);
}

internal sealed class ListLanguagesDsl(
    AcceptanceScenario scenario,
    IListLanguagesProtocolDriver superuserDriver,
    IListLanguagesProtocolDriver standardUserDriver,
    IListLanguagesProtocolDriver anonymousDriver)
{
    private ManagedLanguage? _language;
    private bool _languageWasListed;

    public async Task LanguageExistsAsync(string alias, CancellationToken ct)
    {
        _language = await superuserDriver.CreateLanguageAsync(
            scenario.Data.LanguageName(alias), scenario.Data.LanguageCode(alias), ct);
        scenario.TrackCleanup(token => superuserDriver.DeleteLanguageAsync(_language, token));
    }

    public async Task StandardUserListsLanguagesAsync(CancellationToken ct) =>
        _languageWasListed = await standardUserDriver.IsLanguageListedAsync(
            _language ?? throw new AssertionException("A language is required."), ct);

    public async Task SuperuserListsLanguagesAsync(CancellationToken ct) =>
        _languageWasListed = await superuserDriver.IsLanguageListedAsync(
            _language ?? throw new AssertionException("A language is required."), ct);

    public Task UnauthenticatedPersonAttemptsToListLanguagesAsync(CancellationToken ct) =>
        anonymousDriver.AttemptToListLanguagesWithoutAuthenticationAsync(ct);

    public Task AuthenticationShouldBeRequiredAsync(CancellationToken ct) =>
        anonymousDriver.ListingShouldRequireAuthenticationAsync(ct);

    public Task LanguageShouldBeListedAsync()
    {
        Assert.That(_languageWasListed, Is.True);
        return Task.CompletedTask;
    }
}

internal sealed class GetLanguageDsl(
    AcceptanceScenario scenario,
    IGetLanguageProtocolDriver superuserDriver,
    IGetLanguageProtocolDriver standardUserDriver,
    IGetLanguageProtocolDriver anonymousDriver)
{
    private ManagedLanguage? _language;
    private bool _languageWasViewed;

    public async Task LanguageExistsAsync(string alias, CancellationToken ct)
    {
        _language = await superuserDriver.CreateLanguageAsync(
            scenario.Data.LanguageName(alias), scenario.Data.LanguageCode(alias), ct);
        scenario.TrackCleanup(token => superuserDriver.DeleteLanguageAsync(_language, token));
    }

    public async Task StandardUserViewsLanguageAsync(CancellationToken ct) =>
        _languageWasViewed = await standardUserDriver.CanViewLanguageDetailsAsync(
            _language ?? throw new AssertionException("A language is required."), ct);

    public async Task SuperuserViewsLanguageAsync(CancellationToken ct) =>
        _languageWasViewed = await superuserDriver.CanViewLanguageDetailsAsync(
            _language ?? throw new AssertionException("A language is required."), ct);

    public Task UnauthenticatedPersonAttemptsToViewLanguageAsync(CancellationToken ct) =>
        anonymousDriver.AttemptToViewLanguageWithoutAuthenticationAsync(ct);

    public Task AuthenticationShouldBeRequiredAsync(CancellationToken ct) =>
        anonymousDriver.ViewingShouldRequireAuthenticationAsync(ct);

    public Task LanguageDetailsShouldBeVisibleAsync()
    {
        Assert.That(_languageWasViewed, Is.True);
        return Task.CompletedTask;
    }
}

internal sealed class UpdateLanguageDsl(
    AcceptanceScenario scenario,
    IUpdateLanguageProtocolDriver superuserDriver,
    IUpdateLanguageProtocolDriver standardUserDriver,
    IUpdateLanguageProtocolDriver anonymousDriver)
{
    private ManagedLanguage? _language;
    private ManagedLanguage? _requestedUpdate;

    public async Task LanguageExistsAsync(string alias, CancellationToken ct)
    {
        _language = await superuserDriver.CreateLanguageAsync(
            scenario.Data.LanguageName(alias), scenario.Data.LanguageCode(alias), ct);
        scenario.TrackCleanup(token => superuserDriver.DeleteLanguageAsync(_language, token));
    }

    public async Task SuperuserUpdatesLanguageAsync(string alias, CancellationToken ct)
    {
        _requestedUpdate = UpdatedLanguage(alias);
        await superuserDriver.UpdateLanguageAsync(ExistingLanguage(), _requestedUpdate, ct);
        _language = _requestedUpdate with { Id = _language!.Id };
    }

    public async Task StandardUserAttemptsToUpdateLanguageAsync(string alias, CancellationToken ct)
    {
        _requestedUpdate = UpdatedLanguage(alias);
        await standardUserDriver.AttemptToUpdateLanguageAsync(ExistingLanguage(), _requestedUpdate, ct);
    }

    public Task StandardUserAttemptsToUpdateWithInvalidValuesAsync(CancellationToken ct) =>
        standardUserDriver.AttemptToUpdateLanguageAsync(
            ExistingLanguage(), new ManagedLanguage(null, string.Empty, string.Empty), ct);

    public Task UnauthenticatedPersonAttemptsToUpdateLanguageAsync(CancellationToken ct) =>
        anonymousDriver.AttemptToUpdateLanguageWithoutAuthenticationAsync(ct);

    public Task UpdateShouldBeDeniedAsync(CancellationToken ct) =>
        standardUserDriver.UpdateShouldBeDeniedAsync(ct);

    public Task AuthenticationShouldBeRequiredAsync(CancellationToken ct) =>
        anonymousDriver.UpdateShouldRequireAuthenticationAsync(ct);

    public async Task LanguageShouldHaveRequestedValuesAsync(CancellationToken ct) =>
        Assert.That(
            await superuserDriver.LanguageMatchesAsync(
                ExistingLanguage(),
                _requestedUpdate ?? throw new AssertionException("An update is required."),
                ct),
            Is.True);

    public async Task LanguageShouldRemainUnchangedAsync(CancellationToken ct) =>
        Assert.That(
            await superuserDriver.LanguageMatchesAsync(ExistingLanguage(), ExistingLanguage(), ct),
            Is.True);

    private ManagedLanguage ExistingLanguage() =>
        _language ?? throw new AssertionException("A language is required.");

    private ManagedLanguage UpdatedLanguage(string alias) =>
        new(null, scenario.Data.LanguageName(alias), scenario.Data.LanguageCode(alias));
}

internal sealed class DeleteLanguageDsl(
    AcceptanceScenario scenario,
    IDeleteLanguageProtocolDriver superuserDriver,
    IDeleteLanguageProtocolDriver standardUserDriver,
    IDeleteLanguageProtocolDriver anonymousDriver)
{
    private ManagedLanguage? _language;

    public async Task LanguageExistsAsync(string alias, CancellationToken ct)
    {
        _language = await superuserDriver.CreateLanguageAsync(
            scenario.Data.LanguageName(alias), scenario.Data.LanguageCode(alias), ct);
        scenario.TrackCleanup(token => superuserDriver.CleanupLanguageAsync(_language, token));
    }

    public Task SuperuserDeletesLanguageAsync(CancellationToken ct) =>
        superuserDriver.DeleteLanguageAsync(ExistingLanguage(), ct);

    public Task StandardUserAttemptsToDeleteLanguageAsync(CancellationToken ct) =>
        standardUserDriver.AttemptToDeleteLanguageAsync(ExistingLanguage(), ct);

    public Task UnauthenticatedPersonAttemptsToDeleteLanguageAsync(CancellationToken ct) =>
        anonymousDriver.AttemptToDeleteLanguageWithoutAuthenticationAsync(ct);

    public Task DeletionShouldBeDeniedAsync(CancellationToken ct) =>
        standardUserDriver.DeletionShouldBeDeniedAsync(ct);

    public Task AuthenticationShouldBeRequiredAsync(CancellationToken ct) =>
        anonymousDriver.DeletionShouldRequireAuthenticationAsync(ct);

    public async Task LanguageShouldBeVisibleAsync(CancellationToken ct) =>
        Assert.That(await superuserDriver.IsLanguageVisibleAsync(ExistingLanguage(), ct), Is.True);

    public async Task LanguageShouldNotBeVisibleAsync(CancellationToken ct) =>
        Assert.That(await superuserDriver.IsLanguageVisibleAsync(ExistingLanguage(), ct), Is.False);

    private ManagedLanguage ExistingLanguage() =>
        _language ?? throw new AssertionException("A language is required.");
}

internal sealed record ManagedLanguage(Guid? Id, string Name, string Code);

internal interface ICreateLanguageProtocolDriver : IAsyncDisposable
{
    Task<ManagedLanguage> CreateLanguageAsync(string name, string code, CancellationToken ct);
    Task AttemptToCreateLanguageAsync(ManagedLanguage language, CancellationToken ct);
    Task CreationShouldBeDeniedAsync(CancellationToken ct);
    Task<bool> IsLanguageVisibleAsync(ManagedLanguage language, CancellationToken ct);
    Task DeleteLanguageAsync(ManagedLanguage language, CancellationToken ct);
    Task AttemptToCreateLanguageWithoutAuthenticationAsync(CancellationToken ct);
    Task CreationShouldRequireAuthenticationAsync(CancellationToken ct);
}

internal interface IListLanguagesProtocolDriver : IAsyncDisposable
{
    Task<ManagedLanguage> CreateLanguageAsync(string name, string code, CancellationToken ct);
    Task<bool> IsLanguageListedAsync(ManagedLanguage language, CancellationToken ct);
    Task DeleteLanguageAsync(ManagedLanguage language, CancellationToken ct);
    Task AttemptToListLanguagesWithoutAuthenticationAsync(CancellationToken ct);
    Task ListingShouldRequireAuthenticationAsync(CancellationToken ct);
}

internal interface IGetLanguageProtocolDriver : IAsyncDisposable
{
    Task<ManagedLanguage> CreateLanguageAsync(string name, string code, CancellationToken ct);
    Task<bool> CanViewLanguageDetailsAsync(ManagedLanguage language, CancellationToken ct);
    Task DeleteLanguageAsync(ManagedLanguage language, CancellationToken ct);
    Task AttemptToViewLanguageWithoutAuthenticationAsync(CancellationToken ct);
    Task ViewingShouldRequireAuthenticationAsync(CancellationToken ct);
}

internal interface IUpdateLanguageProtocolDriver : IAsyncDisposable
{
    Task<ManagedLanguage> CreateLanguageAsync(string name, string code, CancellationToken ct);
    Task UpdateLanguageAsync(ManagedLanguage language, ManagedLanguage update, CancellationToken ct);
    Task AttemptToUpdateLanguageAsync(ManagedLanguage language, ManagedLanguage update, CancellationToken ct);
    Task UpdateShouldBeDeniedAsync(CancellationToken ct);
    Task<bool> LanguageMatchesAsync(ManagedLanguage language, ManagedLanguage expected, CancellationToken ct);
    Task DeleteLanguageAsync(ManagedLanguage language, CancellationToken ct);
    Task AttemptToUpdateLanguageWithoutAuthenticationAsync(CancellationToken ct);
    Task UpdateShouldRequireAuthenticationAsync(CancellationToken ct);
}

internal interface IDeleteLanguageProtocolDriver : IAsyncDisposable
{
    Task<ManagedLanguage> CreateLanguageAsync(string name, string code, CancellationToken ct);
    Task DeleteLanguageAsync(ManagedLanguage language, CancellationToken ct);
    Task CleanupLanguageAsync(ManagedLanguage language, CancellationToken ct);
    Task AttemptToDeleteLanguageAsync(ManagedLanguage language, CancellationToken ct);
    Task DeletionShouldBeDeniedAsync(CancellationToken ct);
    Task<bool> IsLanguageVisibleAsync(ManagedLanguage language, CancellationToken ct);
    Task AttemptToDeleteLanguageWithoutAuthenticationAsync(CancellationToken ct);
    Task DeletionShouldRequireAuthenticationAsync(CancellationToken ct);
}
