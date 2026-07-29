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
[AcceptanceProtocols(AcceptanceProtocol.Api)]
/// <summary>Acceptance specification for listing greetings in a time range.</summary>
public sealed class ListGreetingsAcceptanceTests : FeatureFixture
{
    private static readonly DateTimeOffset Start = new(2026, 7, 26, 9, 0, 0, TimeSpan.Zero);
    public static IEnumerable<TestCaseData> Protocols => ProtocolTestCaseSource.For(typeof(ListGreetingsAcceptanceTests));
    private AcceptanceScenario _scenario = null!;
    private IListGreetingsProtocolDriver _driver = null!;
    private ListGreetingsDsl _dsl = null!;

    [SetUp]
    public void SetUp()
    {
        Assert.That(ProtocolTestCaseSource.Current, Is.EqualTo(AcceptanceProtocol.Api));
        _scenario = new AcceptanceScenario(new ScenarioDataContext());
        _driver = new GreetingsApiProtocolDriver(new ApiTransport(AcceptanceSetUp.Options!));
        _dsl = new ListGreetingsDsl(_scenario, _driver);
    }

    [TearDown]
    public async Task TearDown() { await _scenario.DisposeAsync(); await _driver.DisposeAsync(); }

    /// <summary>Shows that a person sees greetings added during the time period they asked for.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_list_can_be_filtered_by_when_greetings_were_introduced(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_business_time_is_the_start_of_the_scenario,
        Given_Japanese_is_supported,
        Given_an_old_greeting_is_introduced,
        Given_business_time_advances_by_25_hours,
        Given_a_new_greeting_is_introduced,
        When_greetings_introduced_in_the_last_24_hours_are_requested,
        Then_the_new_greeting_is_included,
        Then_the_old_greeting_is_not_included);

    private Task Given_business_time_is_the_start_of_the_scenario() => _dsl.SetBusinessTimeAsync(Start, CancellationToken.None);
    private Task Given_Japanese_is_supported() => _dsl.SupportLanguageAsync("Japanese", CancellationToken.None);
    private Task Given_an_old_greeting_is_introduced() => _dsl.IntroduceAsync("おはよう", CancellationToken.None);
    private Task Given_business_time_advances_by_25_hours() => _dsl.SetBusinessTimeAsync(Start.AddHours(25), CancellationToken.None);
    private Task Given_a_new_greeting_is_introduced() => _dsl.IntroduceAsync("こんばんは", CancellationToken.None);
    private Task When_greetings_introduced_in_the_last_24_hours_are_requested() =>
        _dsl.RequestGreetingsIntroducedBetweenAsync(Start.AddHours(1), Start.AddHours(25).AddMicroseconds(1), CancellationToken.None);
    private Task Then_the_new_greeting_is_included() => _dsl.ShouldIncludeAsync("こんばんは");
    private Task Then_the_old_greeting_is_not_included() => _dsl.ShouldNotIncludeAsync("おはよう");

    /// <summary>Shows that a person sees an empty list when no greetings were added during the time period they asked for.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_time_range_with_no_introduced_greetings_is_empty(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_business_time_is_the_start_of_the_scenario,
        Given_Japanese_is_supported,
        Given_a_greeting_is_introduced,
        When_a_later_time_range_is_requested,
        Then_the_list_is_empty);

    /// <summary>Shows that a greeting added at the very start of a time period is included in the results.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_greeting_at_the_inclusive_start_of_a_time_range_is_included(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_business_time_is_the_start_of_the_scenario,
        Given_Japanese_is_supported,
        Given_a_greeting_is_introduced,
        When_a_time_range_starting_at_its_introduction_is_requested,
        Then_the_greeting_is_included);

    private Task Given_a_greeting_is_introduced() => _dsl.IntroduceAsync("こんにちは", CancellationToken.None);
    private Task When_a_later_time_range_is_requested() =>
        _dsl.RequestGreetingsIntroducedBetweenAsync(Start.AddHours(1), Start.AddHours(2), CancellationToken.None);
    private Task Then_the_list_is_empty() => _dsl.ShouldBeEmptyAsync();
    private Task When_a_time_range_starting_at_its_introduction_is_requested() =>
        _dsl.RequestGreetingsIntroducedBetweenAsync(Start, Start.AddMicroseconds(1), CancellationToken.None);
    private Task Then_the_greeting_is_included() => _dsl.ShouldIncludeAsync("こんにちは");
}
