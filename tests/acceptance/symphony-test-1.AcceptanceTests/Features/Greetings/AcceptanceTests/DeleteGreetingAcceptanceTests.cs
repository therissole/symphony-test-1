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
/// <summary>Acceptance specification for deleting a greeting.</summary>
public sealed class DeleteGreetingAcceptanceTests : FeatureFixture
{
    public static IEnumerable<TestCaseData> Protocols => ProtocolTestCaseSource.For(typeof(DeleteGreetingAcceptanceTests));
    private AcceptanceScenario _scenario = null!;
    private IDeleteGreetingAuthorizationProtocolDriver _superuserDriver = null!;
    private IDeleteGreetingAuthorizationProtocolDriver _standardUserDriver = null!;
    private GreetingDeletionAuthorizationDsl _dsl = null!;

    [SetUp]
    public void SetUp()
    {
        var options = AcceptanceSetUp.Options!;
        var protocol = ProtocolTestCaseSource.Current;
        _scenario = new AcceptanceScenario(new ScenarioDataContext());
        _superuserDriver = protocol == AcceptanceProtocol.Api
            ? new GreetingsApiProtocolDriver(new ApiTransport(options, options.SuperuserUserName, options.SuperuserPassword))
            : new GreetingsWebProtocolDriver(new BrowserTransport(options, options.SuperuserUserName, options.SuperuserPassword));
        _standardUserDriver = protocol == AcceptanceProtocol.Api
            ? new GreetingsApiProtocolDriver(new ApiTransport(options, options.StandardUserName, options.StandardUserPassword))
            : new GreetingsWebProtocolDriver(new BrowserTransport(options, options.StandardUserName, options.StandardUserPassword));
        _dsl = new GreetingDeletionAuthorizationDsl(
            _scenario, _superuserDriver, _standardUserDriver);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _scenario.DisposeAsync();
        await _superuserDriver.DisposeAsync();
        await _standardUserDriver.DisposeAsync();
    }

    /// <summary>Shows that the superuser can remove a greeting.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_superuser_can_delete_a_greeting(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_greeting_exists,
        When_the_superuser_deletes_the_greeting,
        Then_the_greeting_is_not_visible);

    /// <summary>Shows that an authenticated standard user cannot remove a greeting.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_standard_user_cannot_delete_a_greeting(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_greeting_exists,
        When_the_standard_user_attempts_to_delete_the_greeting,
        Then_the_action_is_denied,
        Then_the_greeting_remains_visible);

    private async Task Given_a_greeting_exists()
    {
        await _dsl.LanguageExistsAsync("Deletion Japanese", CancellationToken.None);
        await _dsl.SuperuserCreatesGreetingAsync("さようなら", false, CancellationToken.None);
    }

    private Task When_the_superuser_deletes_the_greeting() => _dsl.SuperuserDeletesGreetingAsync(CancellationToken.None);
    private Task When_the_standard_user_attempts_to_delete_the_greeting() => _dsl.StandardUserAttemptsToDeleteGreetingAsync(CancellationToken.None);
    private Task Then_the_action_is_denied() => _dsl.DeletionShouldBeDeniedAsync(CancellationToken.None);
    private Task Then_the_greeting_is_not_visible() => _dsl.GreetingShouldNotBeVisibleAsync(CancellationToken.None);
    private Task Then_the_greeting_remains_visible() => _dsl.GreetingShouldBeVisibleAsync(CancellationToken.None);
}
