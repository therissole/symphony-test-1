using AcceptanceTests.Core;
using AcceptanceTests.Environment;
using AcceptanceTests.Features.Languages.Dsl;
using AcceptanceTests.Features.Languages.ProtocolDrivers;
using AcceptanceTests.TestData;
using LightBDD.Framework.Scenarios;
using LightBDD.NUnit3;

namespace AcceptanceTests.Features.Languages.AcceptanceTests;

[FeatureFixture]
[Category("Acceptance")]
/// <summary>Acceptance specification for deleting a language.</summary>
public sealed class DeleteLanguageAcceptanceTests : FeatureFixture
{
    public static IEnumerable<TestCaseData> Protocols => ProtocolTestCaseSource.For(typeof(DeleteLanguageAcceptanceTests));
    private AcceptanceScenario _scenario = null!;
    private IDeleteLanguageProtocolDriver _superuserDriver = null!;
    private IDeleteLanguageProtocolDriver _standardUserDriver = null!;
    private IDeleteLanguageProtocolDriver _anonymousDriver = null!;
    private DeleteLanguageDsl _dsl = null!;

    [SetUp]
    public void SetUp()
    {
        var options = AcceptanceSetUp.Options!;
        var protocol = ProtocolTestCaseSource.Current;
        _scenario = new AcceptanceScenario(new ScenarioDataContext());
        _superuserDriver = protocol == AcceptanceProtocol.Api
            ? new LanguagesApiProtocolDriver(new ApiTransport(options, options.SuperuserUserName, options.SuperuserPassword))
            : new LanguagesWebProtocolDriver(new BrowserTransport(options, options.SuperuserUserName, options.SuperuserPassword));
        _standardUserDriver = protocol == AcceptanceProtocol.Api
            ? new LanguagesApiProtocolDriver(new ApiTransport(options, options.StandardUserName, options.StandardUserPassword))
            : new LanguagesWebProtocolDriver(new BrowserTransport(options, options.StandardUserName, options.StandardUserPassword));
        _anonymousDriver = protocol == AcceptanceProtocol.Api
            ? new LanguagesApiProtocolDriver(ApiTransport.Anonymous(options))
            : new LanguagesWebProtocolDriver(BrowserTransport.Anonymous(options), options.BaseUri);
        _dsl = new DeleteLanguageDsl(_scenario, _superuserDriver, _standardUserDriver, _anonymousDriver);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _scenario.DisposeAsync();
        await _superuserDriver.DisposeAsync();
        await _standardUserDriver.DisposeAsync();
        await _anonymousDriver.DisposeAsync();
    }

    /// <summary>Shows that the superuser can remove a language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_superuser_can_delete_a_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_language_exists,
        When_the_superuser_deletes_the_language,
        Then_the_language_is_not_visible);

    /// <summary>Shows that an authenticated standard user cannot remove a language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_standard_user_cannot_delete_a_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_language_exists,
        When_the_standard_user_attempts_to_delete_the_language,
        Then_the_action_is_denied,
        Then_the_language_remains_visible);

    /// <summary>Shows that a person must sign in before deleting a language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task An_unauthenticated_person_cannot_delete_a_language(AcceptanceProtocol _) =>
        await Runner.RunScenarioAsync(
            When_an_unauthenticated_person_attempts_to_delete_a_language,
            Then_sign_in_is_required);

    private Task Given_a_language_exists() =>
        _dsl.LanguageExistsAsync("Deleted Polish", CancellationToken.None);

    private Task When_the_superuser_deletes_the_language() =>
        _dsl.SuperuserDeletesLanguageAsync(CancellationToken.None);

    private Task When_the_standard_user_attempts_to_delete_the_language() =>
        _dsl.StandardUserAttemptsToDeleteLanguageAsync(CancellationToken.None);

    private Task When_an_unauthenticated_person_attempts_to_delete_a_language() =>
        _dsl.UnauthenticatedPersonAttemptsToDeleteLanguageAsync(CancellationToken.None);

    private Task Then_the_action_is_denied() => _dsl.DeletionShouldBeDeniedAsync(CancellationToken.None);
    private Task Then_sign_in_is_required() => _dsl.AuthenticationShouldBeRequiredAsync(CancellationToken.None);
    private Task Then_the_language_is_not_visible() => _dsl.LanguageShouldNotBeVisibleAsync(CancellationToken.None);
    private Task Then_the_language_remains_visible() => _dsl.LanguageShouldBeVisibleAsync(CancellationToken.None);
}
