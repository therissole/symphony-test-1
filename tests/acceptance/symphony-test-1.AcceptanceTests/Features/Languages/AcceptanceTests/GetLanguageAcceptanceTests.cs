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
/// <summary>Acceptance specification for viewing one language.</summary>
public sealed class GetLanguageAcceptanceTests : FeatureFixture
{
    public static IEnumerable<TestCaseData> Protocols => ProtocolTestCaseSource.For(typeof(GetLanguageAcceptanceTests));
    private AcceptanceScenario _scenario = null!;
    private IGetLanguageProtocolDriver _superuserDriver = null!;
    private IGetLanguageProtocolDriver _standardUserDriver = null!;
    private IGetLanguageProtocolDriver _anonymousDriver = null!;
    private GetLanguageDsl _dsl = null!;

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
        _dsl = new GetLanguageDsl(_scenario, _superuserDriver, _standardUserDriver, _anonymousDriver);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _scenario.DisposeAsync();
        await _superuserDriver.DisposeAsync();
        await _standardUserDriver.DisposeAsync();
        await _anonymousDriver.DisposeAsync();
    }

    /// <summary>Shows that a standard user can view the details of a language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_standard_user_can_view_a_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_language_exists,
        When_the_standard_user_views_the_language,
        Then_the_language_details_are_visible);

    /// <summary>Shows that the superuser can view the details of a language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_superuser_can_view_a_language(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_language_exists,
        When_the_superuser_views_the_language,
        Then_the_language_details_are_visible);

    /// <summary>Shows that a person must sign in before viewing a language.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task An_unauthenticated_person_cannot_view_a_language(AcceptanceProtocol _) =>
        await Runner.RunScenarioAsync(
            When_an_unauthenticated_person_attempts_to_view_a_language,
            Then_sign_in_is_required);

    private Task Given_a_language_exists() =>
        _dsl.LanguageExistsAsync("Viewed Danish", CancellationToken.None);

    private Task When_the_standard_user_views_the_language() =>
        _dsl.StandardUserViewsLanguageAsync(CancellationToken.None);

    private Task When_the_superuser_views_the_language() =>
        _dsl.SuperuserViewsLanguageAsync(CancellationToken.None);

    private Task When_an_unauthenticated_person_attempts_to_view_a_language() =>
        _dsl.UnauthenticatedPersonAttemptsToViewLanguageAsync(CancellationToken.None);

    private Task Then_sign_in_is_required() => _dsl.AuthenticationShouldBeRequiredAsync(CancellationToken.None);
    private Task Then_the_language_details_are_visible() => _dsl.LanguageDetailsShouldBeVisibleAsync();
}
