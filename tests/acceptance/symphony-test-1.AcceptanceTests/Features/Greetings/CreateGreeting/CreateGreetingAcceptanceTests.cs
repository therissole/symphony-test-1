using AcceptanceTests.Core;
using AcceptanceTests.Environment;
using AcceptanceTests.TestData;
using LightBDD.Framework.Scenarios;
using LightBDD.NUnit3;

namespace AcceptanceTests.Features.Greetings.CreateGreeting;

[FeatureFixture]
[Category("Acceptance")]
public sealed class CreateGreetingAcceptanceTests : FeatureFixture
{
    public static IEnumerable<TestCaseData> Protocols => ProtocolTestCaseSource.For(typeof(CreateGreetingAcceptanceTests));
    private AcceptanceScenario _scenario = null!;
    private ICreateGreetingProtocolDriver _driver = null!;
    private CreateGreetingDsl _dsl = null!;

    [SetUp]
    public void SetUp()
    {
        var protocol = ProtocolTestCaseSource.Current;
        _scenario = new AcceptanceScenario(new ScenarioDataContext());
        _driver = protocol == AcceptanceProtocol.Api
            ? new ApiCreateGreetingProtocolDriver(new ApiTransport(AcceptanceSetUp.Options!))
            : new WebCreateGreetingProtocolDriver(new BrowserTransport(AcceptanceSetUp.Options!));
        _dsl = new CreateGreetingDsl(_scenario, _driver);
    }

    [TearDown]
    public async Task TearDown() { await _scenario.DisposeAsync(); await _driver.DisposeAsync(); }

    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_greeting_can_be_created_for_an_existing_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_Japanese_language_exists,
        When_a_new_informal_greeting_is_created,
        Then_the_greeting_is_visible);

    private Task Given_Japanese_language_exists() => _dsl.LanguageExistsAsync("Japanese", CancellationToken.None);
    private Task When_a_new_informal_greeting_is_created() => _dsl.CreateGreetingAsync("こんにちは", false, CancellationToken.None);
    private Task Then_the_greeting_is_visible() => _dsl.ShouldBeVisibleAsync(CancellationToken.None);
}
