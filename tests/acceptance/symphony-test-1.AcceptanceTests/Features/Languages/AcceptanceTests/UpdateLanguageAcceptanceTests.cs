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
/// <summary>Acceptance specification for updating a language.</summary>
public sealed class UpdateLanguageAcceptanceTests : FeatureFixture
{
    public static IEnumerable<TestCaseData> Protocols => ProtocolTestCaseSource.For(typeof(UpdateLanguageAcceptanceTests));
    private AcceptanceScenario _scenario = null!;
    private IUpdateLanguageProtocolDriver _superuserDriver = null!;
    private IUpdateLanguageProtocolDriver _standardUserDriver = null!;
    private IUpdateLanguageProtocolDriver _anonymousDriver = null!;
    private UpdateLanguageDsl _dsl = null!;

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
        _dsl = new UpdateLanguageDsl(_scenario, _superuserDriver, _standardUserDriver, _anonymousDriver);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _scenario.DisposeAsync();
        await _superuserDriver.DisposeAsync();
        await _standardUserDriver.DisposeAsync();
        await _anonymousDriver.DisposeAsync();
    }

    /// <summary>Shows that the superuser can update a language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_superuser_can_update_a_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_language_exists,
        When_the_superuser_updates_the_language,
        Then_the_language_has_the_requested_values);

    /// <summary>Shows that an authenticated standard user cannot update a language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_standard_user_cannot_update_a_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_language_exists,
        When_the_standard_user_attempts_to_update_the_language,
        Then_the_action_is_denied,
        Then_the_language_is_unchanged);

    /// <summary>Shows that authorization is evaluated before validation for language updates.</summary>
    [Scenario]
    [TestCase(AcceptanceProtocol.Api)]
    public async Task A_standard_user_cannot_discover_language_update_validation_rules(AcceptanceProtocol _) =>
        await Runner.RunScenarioAsync(
            Given_a_language_exists,
            When_the_standard_user_attempts_to_update_the_language_with_invalid_values,
            Then_the_action_is_denied,
            Then_the_language_is_unchanged);

    /// <summary>Shows that a person must sign in before updating a language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task An_unauthenticated_person_cannot_update_a_language(AcceptanceProtocol _) =>
        await Runner.RunScenarioAsync(
            When_an_unauthenticated_person_attempts_to_update_a_language,
            Then_sign_in_is_required);

    private Task Given_a_language_exists() =>
        _dsl.LanguageExistsAsync("Original Dutch", CancellationToken.None);

    private Task When_the_superuser_updates_the_language() =>
        _dsl.SuperuserUpdatesLanguageAsync("Updated Dutch", CancellationToken.None);

    private Task When_the_standard_user_attempts_to_update_the_language() =>
        _dsl.StandardUserAttemptsToUpdateLanguageAsync("Denied Dutch", CancellationToken.None);

    private Task When_the_standard_user_attempts_to_update_the_language_with_invalid_values() =>
        _dsl.StandardUserAttemptsToUpdateWithInvalidValuesAsync(CancellationToken.None);

    private Task When_an_unauthenticated_person_attempts_to_update_a_language() =>
        _dsl.UnauthenticatedPersonAttemptsToUpdateLanguageAsync(CancellationToken.None);

    private Task Then_the_action_is_denied() => _dsl.UpdateShouldBeDeniedAsync(CancellationToken.None);
    private Task Then_sign_in_is_required() => _dsl.AuthenticationShouldBeRequiredAsync(CancellationToken.None);
    private Task Then_the_language_has_the_requested_values() => _dsl.LanguageShouldHaveRequestedValuesAsync(CancellationToken.None);
    private Task Then_the_language_is_unchanged() => _dsl.LanguageShouldRemainUnchangedAsync(CancellationToken.None);
}
