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
/// <summary>Acceptance specification for updating a greeting.</summary>
public sealed class UpdateGreetingAcceptanceTests : FeatureFixture
{
    public static IEnumerable<TestCaseData> Protocols =>
        ProtocolTestCaseSource.For(typeof(UpdateGreetingAcceptanceTests));

    private AcceptanceScenario _scenario = null!;
    private IUpdateGreetingAuthorizationProtocolDriver _superuserDriver = null!;
    private IUpdateGreetingAuthorizationProtocolDriver _standardUserDriver = null!;
    private UpdateGreetingAuthorizationDsl _dsl = null!;
    private IUpdateGreetingAuthenticationProtocolDriver _unauthenticatedDriver = null!;
    private UpdateGreetingAuthenticationDsl _authenticationDsl = null!;

    [SetUp]
    public void SetUp()
    {
        var options = AcceptanceSetUp.Options!;
        var protocol = ProtocolTestCaseSource.Current;
        _scenario = new AcceptanceScenario(new ScenarioDataContext());
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
        _dsl = new UpdateGreetingAuthorizationDsl(
            _scenario, _superuserDriver, _standardUserDriver);
        _authenticationDsl = new UpdateGreetingAuthenticationDsl(_unauthenticatedDriver);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _scenario.DisposeAsync();
        await _superuserDriver.DisposeAsync();
        await _standardUserDriver.DisposeAsync();
        await _unauthenticatedDriver.DisposeAsync();
    }

    /// <summary>Shows that the superuser can update a greeting.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_superuser_can_update_a_greeting(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_greeting_exists,
        When_the_superuser_updates_the_greeting,
        Then_the_greeting_contains_the_update);

    /// <summary>Shows that a standard user cannot update a greeting.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_standard_user_cannot_update_a_greeting(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_greeting_exists,
        When_the_standard_user_attempts_to_update_the_greeting,
        Then_the_greeting_update_is_denied,
        Then_the_greeting_remains_unchanged);

    /// <summary>Shows that authorization is evaluated before validating a standard user's update.</summary>
    [Scenario]
    [TestCase(AcceptanceProtocol.Api)]
    public async Task A_standard_user_cannot_discover_greeting_update_validation_rules(AcceptanceProtocol _) =>
        await Runner.RunScenarioAsync(
            Given_a_greeting_exists,
            When_the_standard_user_attempts_an_invalid_update,
            Then_the_greeting_update_is_denied,
            Then_the_greeting_remains_unchanged);

    /// <summary>Shows that a person must sign in before updating a greeting.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task An_unauthenticated_person_cannot_update_a_greeting(AcceptanceProtocol _) =>
        await Runner.RunScenarioAsync(
            When_an_unauthenticated_person_attempts_to_update_a_greeting,
            Then_authentication_is_required_and_greeting_update_is_unavailable);

    private Task Given_a_greeting_exists() =>
        _dsl.GreetingExistsAsync("Updating Korean", "안녕하세요", false, CancellationToken.None);

    private Task When_the_superuser_updates_the_greeting() =>
        _dsl.SuperuserUpdatesGreetingAsync("안녕하십니까", true, CancellationToken.None);

    private Task When_the_standard_user_attempts_to_update_the_greeting() =>
        _dsl.StandardUserAttemptsToUpdateGreetingAsync("변경", true, CancellationToken.None);

    private Task When_the_standard_user_attempts_an_invalid_update() =>
        _dsl.StandardUserAttemptsToUpdateGreetingAsync(string.Empty, true, CancellationToken.None);

    private Task Then_the_greeting_update_is_denied() =>
        _dsl.UpdateShouldBeDeniedAsync(CancellationToken.None);

    private Task Then_the_greeting_contains_the_update() =>
        _dsl.GreetingShouldContainRequestedUpdateAsync(CancellationToken.None);

    private Task Then_the_greeting_remains_unchanged() =>
        _dsl.GreetingShouldRemainUnchangedAsync(CancellationToken.None);

    private Task When_an_unauthenticated_person_attempts_to_update_a_greeting() =>
        _authenticationDsl.UnauthenticatedPersonAttemptsToUpdateGreetingAsync(CancellationToken.None);

    private Task Then_authentication_is_required_and_greeting_update_is_unavailable() =>
        _authenticationDsl.AuthenticationShouldBeRequiredAndUpdateUnavailableAsync(CancellationToken.None);
}
