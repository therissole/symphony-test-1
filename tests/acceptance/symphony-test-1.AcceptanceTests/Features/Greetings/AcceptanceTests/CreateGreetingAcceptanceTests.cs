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

    [SetUp]
    public void SetUp()
    {
        var protocol = ProtocolTestCaseSource.Current;
        _scenario = new AcceptanceScenario(new ScenarioDataContext());
        _driver = protocol == AcceptanceProtocol.Api
            ? new GreetingsApiProtocolDriver(new ApiTransport(AcceptanceSetUp.Options!))
            : new GreetingsWebProtocolDriver(new BrowserTransport(AcceptanceSetUp.Options!));
        _dsl = new GreetingsDsl(_scenario, _driver);
    }

    [TearDown]
    public async Task TearDown() { await _scenario.DisposeAsync(); await _driver.DisposeAsync(); }

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
        When_an_informal_greeting_is_created,
        When_a_formal_greeting_is_created,
        Then_both_greetings_are_visible);

    private Task When_a_new_formal_greeting_is_created() => _dsl.CreateGreetingAsync("よろしくお願いいたします", true, CancellationToken.None);
    private Task When_an_informal_greeting_is_created() => _dsl.CreateGreetingAsync("やあ", false, CancellationToken.None);
    private Task When_a_formal_greeting_is_created() => _dsl.CreateGreetingAsync("こんにちは", true, CancellationToken.None);
    private async Task Then_both_greetings_are_visible()
    {
        await _dsl.ShouldGreetingBeVisibleAsync("やあ", false, CancellationToken.None);
        await _dsl.ShouldGreetingBeVisibleAsync("こんにちは", true, CancellationToken.None);
    }
}
