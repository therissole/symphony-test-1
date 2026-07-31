using AcceptanceTests.Core;
using AcceptanceTests.Environment;
using AcceptanceTests.Features.Greetings.Dsl;
using AcceptanceTests.Features.Greetings.ProtocolDrivers;
using AcceptanceTests.TestData;
using LightBDD.Framework.Scenarios;
using LightBDD.NUnit3;

namespace AcceptanceTests.Features.Greetings.AcceptanceTests;

[FeatureFixture]
[Category("Acceptance")]
/// <summary>Acceptance specification for creating a greeting.</summary>
public sealed class CreateGreetingAcceptanceTests : FeatureFixture
{
    public static IEnumerable<TestCaseData> Protocols => ProtocolTestCaseSource.For(typeof(CreateGreetingAcceptanceTests));
    private AcceptanceScenario _scenario = null!;
    private ICreateGreetingProtocolDriver _driver = null!;
    private GreetingsDsl _dsl = null!;
    private ICreateGreetingProtocolDriver _superuserDriver = null!;
    private ICreateGreetingAuthorizationProtocolDriver _standardUserDriver = null!;
    private GreetingCreationAuthorizationDsl _authorizationDsl = null!;
    private ICreateGreetingAuthenticationProtocolDriver _unauthenticatedDriver = null!;
    private CreateGreetingAuthenticationDsl _authenticationDsl = null!;

    [SetUp]
    public void SetUp()
    {
        var protocol = ProtocolTestCaseSource.Current;
        _scenario = new AcceptanceScenario(new ScenarioDataContext());
        _driver = protocol == AcceptanceProtocol.Api
            ? new GreetingsApiProtocolDriver(new ApiTransport(
                AcceptanceSetUp.Options!,
                AcceptanceSetUp.Options!.SuperuserUserName,
                AcceptanceSetUp.Options!.SuperuserPassword))
            : new GreetingsWebProtocolDriver(new BrowserTransport(AcceptanceSetUp.Options!));
        _dsl = new GreetingsDsl(_scenario, _driver);

        var options = AcceptanceSetUp.Options!;
        _superuserDriver = protocol == AcceptanceProtocol.Api
            ? new GreetingsApiProtocolDriver(new ApiTransport(
                options, options.SuperuserUserName, options.SuperuserPassword))
            : new GreetingsWebProtocolDriver(new BrowserTransport(
                options, options.SuperuserUserName, options.SuperuserPassword));
        _standardUserDriver = protocol == AcceptanceProtocol.Api
            ? new GreetingsApiProtocolDriver(new ApiTransport(
                options, options.StandardUserName, options.StandardUserPassword))
            : new GreetingsWebProtocolDriver(new BrowserTransport(
                options, options.StandardUserName, options.StandardUserPassword));
        _unauthenticatedDriver = protocol == AcceptanceProtocol.Api
            ? new GreetingsApiProtocolDriver(ApiTransport.Anonymous(options))
            : new GreetingsWebProtocolDriver(BrowserTransport.Anonymous(options), options.BaseUri);
        _authorizationDsl = new GreetingCreationAuthorizationDsl(
            _scenario, _superuserDriver, _standardUserDriver);
        _authenticationDsl = new CreateGreetingAuthenticationDsl(_unauthenticatedDriver);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _scenario.DisposeAsync();
        await _driver.DisposeAsync();
        if (_superuserDriver is not null) await _superuserDriver.DisposeAsync();
        if (_standardUserDriver is not null) await _standardUserDriver.DisposeAsync();
        await _unauthenticatedDriver.DisposeAsync();
    }

    /// <summary>Shows that a person can add an everyday greeting for a language they already use.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_greeting_can_be_created_for_an_existing_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_Japanese_language_exists,
        When_a_new_informal_greeting_is_created,
        Then_the_greeting_is_visible);

    private Task Given_Japanese_language_exists() => _dsl.LanguageExistsAsync("Japanese", CancellationToken.None);
    private Task When_a_new_informal_greeting_is_created() => _dsl.CreateGreetingAsync("こんにちは", false, CancellationToken.None);
    private Task Then_the_greeting_is_visible() => _dsl.ShouldBeVisibleAsync(CancellationToken.None);

    /// <summary>Shows that a person can add a greeting intended for formal situations.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_formal_greeting_can_be_created_for_an_existing_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_Japanese_language_exists,
        When_a_new_formal_greeting_is_created,
        Then_the_greeting_is_visible);

    /// <summary>Shows that adding one greeting does not prevent a person from adding another for the same language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task Multiple_greetings_can_be_created_for_the_same_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_Japanese_language_exists,
        Given_an_informal_greeting_exists,
        When_a_formal_greeting_is_created,
        Then_both_greetings_are_visible);

    private Task When_a_new_formal_greeting_is_created() => _dsl.CreateGreetingAsync("よろしくお願いいたします", true, CancellationToken.None);
    private Task Given_an_informal_greeting_exists() => _dsl.CreateGreetingAsync("やあ", false, CancellationToken.None);
    private Task When_a_formal_greeting_is_created() => _dsl.CreateGreetingAsync("こんにちは", true, CancellationToken.None);
    private async Task Then_both_greetings_are_visible()
    {
        await _dsl.ShouldGreetingBeVisibleAsync("やあ", false, CancellationToken.None);
        await _dsl.ShouldGreetingBeVisibleAsync("こんにちは", true, CancellationToken.None);
    }

    /// <summary>Shows that the superuser may create a greeting for an existing language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_superuser_can_create_a_greeting(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_language_exists_for_authorization,
        When_the_superuser_creates_a_greeting,
        Then_the_superuser_can_see_the_greeting);

    /// <summary>Shows that an authenticated standard user cannot create a greeting.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_standard_user_cannot_create_a_greeting(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_language_exists_for_authorization,
        When_the_standard_user_attempts_to_create_a_greeting,
        Then_the_greeting_creation_is_denied,
        Then_the_greeting_was_not_created);

    /// <summary>Shows that authorization is evaluated before validating a standard user's request.</summary>
    [Scenario]
    [TestCase(AcceptanceProtocol.Api)]
    public async Task A_standard_user_cannot_discover_greeting_validation_rules(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_language_exists_for_authorization,
        When_the_standard_user_attempts_to_create_an_invalid_greeting,
        Then_the_greeting_creation_is_denied);

    /// <summary>Shows that a person must sign in before creating a greeting.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task An_unauthenticated_person_cannot_create_a_greeting(AcceptanceProtocol _) =>
        await Runner.RunScenarioAsync(
            When_an_unauthenticated_person_attempts_to_create_a_greeting,
            Then_authentication_is_required_and_greeting_creation_is_unavailable);

    private Task Given_a_language_exists_for_authorization() =>
        _authorizationDsl.LanguageExistsAsync("Authorization Japanese", CancellationToken.None);
    private Task When_the_superuser_creates_a_greeting() =>
        _authorizationDsl.SuperuserCreatesGreetingAsync("お元気ですか", false, CancellationToken.None);
    private Task Then_the_superuser_can_see_the_greeting() =>
        _authorizationDsl.SuperuserCanSeeGreetingAsync(CancellationToken.None);
    private Task When_the_standard_user_attempts_to_create_a_greeting() =>
        _authorizationDsl.StandardUserAttemptsToCreateGreetingAsync("失礼します", true, CancellationToken.None);
    private Task When_the_standard_user_attempts_to_create_an_invalid_greeting() =>
        _authorizationDsl.StandardUserAttemptsToCreateGreetingAsync(string.Empty, false, CancellationToken.None);
    private Task Then_the_greeting_creation_is_denied() =>
        _authorizationDsl.CreationShouldBeDeniedAsync(CancellationToken.None);
    private Task Then_the_greeting_was_not_created() =>
        _authorizationDsl.AttemptedGreetingShouldNotBeVisibleAsync(CancellationToken.None);
    private Task When_an_unauthenticated_person_attempts_to_create_a_greeting() =>
        _authenticationDsl.UnauthenticatedPersonAttemptsToCreateGreetingAsync(CancellationToken.None);
    private Task Then_authentication_is_required_and_greeting_creation_is_unavailable() =>
        _authenticationDsl.AuthenticationShouldBeRequiredAndCreationUnavailableAsync(CancellationToken.None);
}
