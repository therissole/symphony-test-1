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
[AcceptanceProtocols(AcceptanceProtocol.Api)]
/// <summary>
/// Acceptance specification for the API-only request that gets one greeting by language.
/// </summary>
public sealed class GetGreetingByLanguageAcceptanceTests : FeatureFixture
{
    public static IEnumerable<TestCaseData> Protocols =>
        ProtocolTestCaseSource.For(typeof(GetGreetingByLanguageAcceptanceTests));

    private AcceptanceScenario _scenario = null!;
    private IGetGreetingByLanguageAuthorizationProtocolDriver _superuserDriver = null!;
    private IGetGreetingByLanguageAuthorizationProtocolDriver _standardUserDriver = null!;
    private GetGreetingByLanguageAuthorizationDsl _dsl = null!;
    private IGetGreetingByLanguageAuthenticationProtocolDriver _unauthenticatedDriver = null!;
    private GetGreetingByLanguageAuthenticationDsl _authenticationDsl = null!;

    [SetUp]
    public void SetUp()
    {
        var options = AcceptanceSetUp.Options!;
        _scenario = new AcceptanceScenario(new ScenarioDataContext());
        _superuserDriver = new GreetingsApiProtocolDriver(new ApiTransport(
            options, options.SuperuserUserName, options.SuperuserPassword));
        _standardUserDriver = new GreetingsApiProtocolDriver(new ApiTransport(
            options, options.StandardUserName, options.StandardUserPassword));
        _unauthenticatedDriver = new GreetingsApiProtocolDriver(ApiTransport.Anonymous(options));
        _dsl = new GetGreetingByLanguageAuthorizationDsl(
            _scenario, _superuserDriver, _standardUserDriver);
        _authenticationDsl = new GetGreetingByLanguageAuthenticationDsl(_unauthenticatedDriver);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _scenario.DisposeAsync();
        await _superuserDriver.DisposeAsync();
        await _standardUserDriver.DisposeAsync();
        await _unauthenticatedDriver.DisposeAsync();
    }

    /// <summary>Shows that the superuser can get a greeting by its language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_superuser_can_get_a_greeting_by_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_greeting_exists,
        When_the_superuser_gets_a_greeting_by_language,
        Then_the_greeting_details_are_visible);

    /// <summary>Shows that a standard user can get a greeting by its language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_standard_user_can_get_a_greeting_by_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_greeting_exists,
        When_the_standard_user_gets_a_greeting_by_language,
        Then_the_greeting_details_are_visible);

    /// <summary>Shows that a person must sign in before finding a greeting by language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task An_unauthenticated_person_cannot_get_a_greeting_by_language(AcceptanceProtocol _) =>
        await Runner.RunScenarioAsync(
            When_an_unauthenticated_person_attempts_to_find_a_greeting_by_language,
            Then_authentication_is_required_and_the_greeting_details_are_unavailable);

    private Task Given_a_greeting_exists() =>
        _dsl.GreetingExistsAsync("Lookup Spanish", "Hola", false, CancellationToken.None);

    private Task When_the_superuser_gets_a_greeting_by_language() =>
        _dsl.SuperuserGetsGreetingByLanguageAsync(CancellationToken.None);

    private Task When_the_standard_user_gets_a_greeting_by_language() =>
        _dsl.StandardUserGetsGreetingByLanguageAsync(CancellationToken.None);

    private Task Then_the_greeting_details_are_visible() =>
        _dsl.GreetingDetailsShouldBeVisibleAsync();

    private Task When_an_unauthenticated_person_attempts_to_find_a_greeting_by_language() =>
        _authenticationDsl.UnauthenticatedPersonAttemptsToFindGreetingByLanguageAsync(CancellationToken.None);

    private Task Then_authentication_is_required_and_the_greeting_details_are_unavailable() =>
        _authenticationDsl.AuthenticationShouldBeRequiredAndDetailsUnavailableAsync(CancellationToken.None);
}
