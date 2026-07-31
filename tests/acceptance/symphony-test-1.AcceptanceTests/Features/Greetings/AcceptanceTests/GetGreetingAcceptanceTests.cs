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
/// <summary>Acceptance specification for getting one greeting.</summary>
public sealed class GetGreetingAcceptanceTests : FeatureFixture
{
    public static IEnumerable<TestCaseData> Protocols =>
        ProtocolTestCaseSource.For(typeof(GetGreetingAcceptanceTests));

    private AcceptanceScenario _scenario = null!;
    private IGetGreetingAuthorizationProtocolDriver _superuserDriver = null!;
    private IGetGreetingAuthorizationProtocolDriver _standardUserDriver = null!;
    private GetGreetingAuthorizationDsl _dsl = null!;
    private IGetGreetingAuthenticationProtocolDriver _unauthenticatedDriver = null!;
    private GetGreetingAuthenticationDsl _authenticationDsl = null!;

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
        _dsl = new GetGreetingAuthorizationDsl(_scenario, _superuserDriver, _standardUserDriver);
        _authenticationDsl = new GetGreetingAuthenticationDsl(_unauthenticatedDriver);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _scenario.DisposeAsync();
        await _superuserDriver.DisposeAsync();
        await _standardUserDriver.DisposeAsync();
        await _unauthenticatedDriver.DisposeAsync();
    }

    /// <summary>Shows that the superuser can view one greeting.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_superuser_can_get_a_greeting(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_greeting_exists,
        When_the_superuser_gets_the_greeting,
        Then_the_greeting_details_are_visible);

    /// <summary>Shows that a standard user can view one greeting.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_standard_user_can_get_a_greeting(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_greeting_exists,
        When_the_standard_user_gets_the_greeting,
        Then_the_greeting_details_are_visible);

    /// <summary>Shows that a person must sign in before viewing a greeting.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task An_unauthenticated_person_cannot_get_a_greeting(AcceptanceProtocol _) =>
        await Runner.RunScenarioAsync(
            When_an_unauthenticated_person_attempts_to_get_a_greeting,
            Then_authentication_is_required_and_the_greeting_details_are_unavailable);

    private Task Given_a_greeting_exists() =>
        _dsl.GreetingExistsAsync("Viewing Japanese", "こんにちは", false, CancellationToken.None);

    private Task When_the_superuser_gets_the_greeting() =>
        _dsl.SuperuserGetsGreetingAsync(CancellationToken.None);

    private Task When_the_standard_user_gets_the_greeting() =>
        _dsl.StandardUserGetsGreetingAsync(CancellationToken.None);

    private Task Then_the_greeting_details_are_visible() =>
        _dsl.GreetingDetailsShouldBeVisibleAsync();

    private Task When_an_unauthenticated_person_attempts_to_get_a_greeting() =>
        _authenticationDsl.UnauthenticatedPersonAttemptsToViewGreetingAsync(CancellationToken.None);

    private Task Then_authentication_is_required_and_the_greeting_details_are_unavailable() =>
        _authenticationDsl.AuthenticationShouldBeRequiredAndDetailsUnavailableAsync(CancellationToken.None);
}
