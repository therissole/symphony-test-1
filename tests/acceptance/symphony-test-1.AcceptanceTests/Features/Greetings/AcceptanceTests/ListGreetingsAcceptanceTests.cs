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
[NonParallelizable]
/// <summary>Acceptance specification for listing greetings.</summary>
public sealed class ListGreetingsAcceptanceTests : FeatureFixture
{
    private static readonly DateTimeOffset Start = new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);
    public static IEnumerable<TestCaseData> Protocols => ProtocolTestCaseSource.For(typeof(ListGreetingsAcceptanceTests));
    private AcceptanceScenario _scenario = null!;
    private IListGreetingsAuthorizationProtocolDriver _superuserDriver = null!;
    private IListGreetingsAuthorizationProtocolDriver _standardUserDriver = null!;
    private ListGreetingsDsl? _timeRangeDsl;
    private ListGreetingsAuthorizationDsl _authorizationDsl = null!;
    private IListGreetingsAuthenticationProtocolDriver _unauthenticatedDriver = null!;
    private ListGreetingsAuthenticationDsl _authenticationDsl = null!;

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
        _authorizationDsl = new ListGreetingsAuthorizationDsl(
            _scenario, _superuserDriver, _standardUserDriver);
        _authenticationDsl = new ListGreetingsAuthenticationDsl(_unauthenticatedDriver);
        _timeRangeDsl = protocol == AcceptanceProtocol.Api
            ? new ListGreetingsDsl(_scenario, (IListGreetingsProtocolDriver)_superuserDriver)
            : null;
    }

    [TearDown]
    public async Task TearDown()
    {
        await _scenario.DisposeAsync();
        await _superuserDriver.DisposeAsync();
        await _standardUserDriver.DisposeAsync();
        await _unauthenticatedDriver.DisposeAsync();
    }

    /// <summary>Shows that a person sees greetings added during the time period they asked for.</summary>
    [Scenario]
    [TestCase(AcceptanceProtocol.Api)]
    public async Task A_list_can_be_filtered_by_when_greetings_were_introduced(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_business_time_is_the_start_of_the_scenario,
        Given_Japanese_is_supported,
        Given_an_old_greeting_is_introduced,
        Given_business_time_advances_by_25_hours,
        Given_a_new_greeting_is_introduced,
        When_greetings_introduced_in_the_last_24_hours_are_requested,
        Then_the_new_greeting_is_included,
        Then_the_old_greeting_is_not_included);

    private ListGreetingsDsl TimeRangeDsl =>
        _timeRangeDsl ?? throw new AssertionException("Time-range scenarios require the API protocol.");

    private Task Given_business_time_is_the_start_of_the_scenario() => TimeRangeDsl.SetBusinessTimeAsync(Start, CancellationToken.None);
    private Task Given_Japanese_is_supported() => TimeRangeDsl.SupportLanguageAsync("Japanese", CancellationToken.None);
    private Task Given_an_old_greeting_is_introduced() => TimeRangeDsl.IntroduceAsync("おはよう", CancellationToken.None);
    private Task Given_business_time_advances_by_25_hours() => TimeRangeDsl.SetBusinessTimeAsync(Start.AddHours(25), CancellationToken.None);
    private Task Given_a_new_greeting_is_introduced() => TimeRangeDsl.IntroduceAsync("こんばんは", CancellationToken.None);
    private Task When_greetings_introduced_in_the_last_24_hours_are_requested() =>
        TimeRangeDsl.RequestGreetingsIntroducedBetweenAsync(Start.AddHours(1), Start.AddHours(25).AddMicroseconds(1), CancellationToken.None);
    private Task Then_the_new_greeting_is_included() => TimeRangeDsl.ShouldIncludeAsync("こんばんは");
    private Task Then_the_old_greeting_is_not_included() => TimeRangeDsl.ShouldNotIncludeAsync("おはよう");

    /// <summary>Shows that a person sees an empty list when no greetings were added during the time period they asked for.</summary>
    [Scenario]
    [TestCase(AcceptanceProtocol.Api)]
    public async Task A_time_range_with_no_introduced_greetings_is_empty(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_business_time_is_the_start_of_the_scenario,
        Given_Japanese_is_supported,
        Given_a_greeting_is_introduced,
        When_a_later_time_range_is_requested,
        Then_the_list_is_empty);

    /// <summary>Shows that a greeting added at the very start of a time period is included in the results.</summary>
    [Scenario]
    [TestCase(AcceptanceProtocol.Api)]
    public async Task A_greeting_at_the_inclusive_start_of_a_time_range_is_included(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_business_time_is_the_start_of_the_scenario,
        Given_Japanese_is_supported,
        Given_a_greeting_is_introduced,
        When_a_time_range_starting_at_its_introduction_is_requested,
        Then_the_greeting_is_included);

    private Task Given_a_greeting_is_introduced() => TimeRangeDsl.IntroduceAsync("こんにちは", CancellationToken.None);
    private Task When_a_later_time_range_is_requested() =>
        TimeRangeDsl.RequestGreetingsIntroducedBetweenAsync(Start.AddHours(1), Start.AddHours(2), CancellationToken.None);
    private Task Then_the_list_is_empty() => TimeRangeDsl.ShouldBeEmptyAsync();
    private Task When_a_time_range_starting_at_its_introduction_is_requested() =>
        TimeRangeDsl.RequestGreetingsIntroducedBetweenAsync(Start, Start.AddMicroseconds(1), CancellationToken.None);
    private Task Then_the_greeting_is_included() => TimeRangeDsl.ShouldIncludeAsync("こんにちは");

    /// <summary>Shows that the superuser can list greetings.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_superuser_can_list_greetings(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_greeting_exists_for_authorization,
        When_the_superuser_lists_greetings,
        Then_the_greeting_is_listed);

    /// <summary>Shows that a standard user can list greetings.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_standard_user_can_list_greetings(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_greeting_exists_for_authorization,
        When_the_standard_user_lists_greetings,
        Then_the_greeting_is_listed);

    /// <summary>Shows that a person must sign in before listing greetings.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task An_unauthenticated_person_cannot_list_greetings(AcceptanceProtocol _) =>
        await Runner.RunScenarioAsync(
            When_an_unauthenticated_person_attempts_to_list_greetings,
            Then_authentication_is_required_and_the_greeting_list_is_unavailable);

    private Task Given_a_greeting_exists_for_authorization() =>
        _authorizationDsl.GreetingExistsAsync(
            "Authorization Korean", "안녕하세요", false, CancellationToken.None);
    private Task When_the_superuser_lists_greetings() =>
        _authorizationDsl.SuperuserListsGreetingsAsync(CancellationToken.None);
    private Task When_the_standard_user_lists_greetings() =>
        _authorizationDsl.StandardUserListsGreetingsAsync(CancellationToken.None);
    private Task Then_the_greeting_is_listed() =>
        _authorizationDsl.GreetingShouldBeListedAsync();
    private Task When_an_unauthenticated_person_attempts_to_list_greetings() =>
        _authenticationDsl.UnauthenticatedPersonAttemptsToListGreetingsAsync(CancellationToken.None);
    private Task Then_authentication_is_required_and_the_greeting_list_is_unavailable() =>
        _authenticationDsl.AuthenticationShouldBeRequiredAndListUnavailableAsync(CancellationToken.None);
}
