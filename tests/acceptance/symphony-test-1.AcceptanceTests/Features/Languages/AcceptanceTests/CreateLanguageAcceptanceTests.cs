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
/// <summary>Acceptance specification for creating a language.</summary>
public sealed class CreateLanguageAcceptanceTests : FeatureFixture
{
    public static IEnumerable<TestCaseData> Protocols => ProtocolTestCaseSource.For(typeof(CreateLanguageAcceptanceTests));
    private AcceptanceScenario _scenario = null!;
    private ICreateLanguageProtocolDriver _superuserDriver = null!;
    private ICreateLanguageProtocolDriver _standardUserDriver = null!;
    private ICreateLanguageProtocolDriver _anonymousDriver = null!;
    private CreateLanguageDsl _dsl = null!;

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
        _dsl = new CreateLanguageDsl(_scenario, _superuserDriver, _standardUserDriver, _anonymousDriver);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _scenario.DisposeAsync();
        await _superuserDriver.DisposeAsync();
        await _standardUserDriver.DisposeAsync();
        await _anonymousDriver.DisposeAsync();
    }

    /// <summary>Shows that the superuser can add a language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_superuser_can_create_a_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        When_the_superuser_creates_a_language,
        Then_the_language_is_visible);

    /// <summary>Shows that an authenticated standard user cannot add a language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_standard_user_cannot_create_a_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        When_the_standard_user_attempts_to_create_a_language,
        Then_the_action_is_denied,
        Then_the_language_is_not_visible);

    /// <summary>Shows that authorization is evaluated before validation for language creation.</summary>
    [Scenario]
    [TestCase(AcceptanceProtocol.Api)]
    public async Task A_standard_user_cannot_discover_language_creation_validation_rules(AcceptanceProtocol _) =>
        await Runner.RunScenarioAsync(
            When_the_standard_user_attempts_to_create_an_invalid_language,
            Then_the_action_is_denied);

    /// <summary>Shows that a person must sign in before creating a language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task An_unauthenticated_person_cannot_create_a_language(AcceptanceProtocol _) =>
        await Runner.RunScenarioAsync(
            When_an_unauthenticated_person_attempts_to_create_a_language,
            Then_sign_in_is_required);

    private Task When_the_superuser_creates_a_language() =>
        _dsl.SuperuserCreatesLanguageAsync("Created Norwegian", CancellationToken.None);

    private Task When_the_standard_user_attempts_to_create_a_language() =>
        _dsl.StandardUserAttemptsToCreateLanguageAsync("Denied Norwegian", CancellationToken.None);

    private Task When_the_standard_user_attempts_to_create_an_invalid_language() =>
        _dsl.StandardUserAttemptsToCreateInvalidLanguageAsync(CancellationToken.None);

    private Task When_an_unauthenticated_person_attempts_to_create_a_language() =>
        _dsl.UnauthenticatedPersonAttemptsToCreateLanguageAsync(CancellationToken.None);

    private Task Then_the_action_is_denied() => _dsl.CreationShouldBeDeniedAsync(CancellationToken.None);
    private Task Then_sign_in_is_required() => _dsl.AuthenticationShouldBeRequiredAsync(CancellationToken.None);
    private Task Then_the_language_is_visible() => _dsl.LanguageShouldBeVisibleAsync(CancellationToken.None);
    private Task Then_the_language_is_not_visible() => _dsl.LanguageShouldNotBeVisibleAsync(CancellationToken.None);
}
