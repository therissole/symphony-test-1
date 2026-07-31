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
/// <summary>Acceptance specification for listing languages.</summary>
public sealed class ListLanguagesAcceptanceTests : FeatureFixture
{
    public static IEnumerable<TestCaseData> Protocols => ProtocolTestCaseSource.For(typeof(ListLanguagesAcceptanceTests));
    private AcceptanceScenario _scenario = null!;
    private IListLanguagesProtocolDriver _superuserDriver = null!;
    private IListLanguagesProtocolDriver _standardUserDriver = null!;
    private IListLanguagesProtocolDriver _anonymousDriver = null!;
    private ListLanguagesDsl _dsl = null!;

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
        _dsl = new ListLanguagesDsl(_scenario, _superuserDriver, _standardUserDriver, _anonymousDriver);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _scenario.DisposeAsync();
        await _superuserDriver.DisposeAsync();
        await _standardUserDriver.DisposeAsync();
        await _anonymousDriver.DisposeAsync();
    }

    /// <summary>Shows that a standard user can list the available languages.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_standard_user_can_list_languages(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_language_exists,
        When_the_standard_user_lists_languages,
        Then_the_language_is_listed);

    /// <summary>Shows that the superuser can list the available languages.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task A_superuser_can_list_languages(AcceptanceProtocol _) => await Runner.RunScenarioAsync(
        Given_a_language_exists,
        When_the_superuser_lists_languages,
        Then_the_language_is_listed);

    /// <summary>Shows that a person must sign in before listing languages.</summary>
    [Scenario]
    [TestCaseSource(nameof(Protocols))]
    public async Task An_unauthenticated_person_cannot_list_languages(AcceptanceProtocol _) =>
        await Runner.RunScenarioAsync(
            When_an_unauthenticated_person_attempts_to_list_languages,
            Then_sign_in_is_required);

    private Task Given_a_language_exists() =>
        _dsl.LanguageExistsAsync("Listed Finnish", CancellationToken.None);

    private Task When_the_standard_user_lists_languages() =>
        _dsl.StandardUserListsLanguagesAsync(CancellationToken.None);

    private Task When_the_superuser_lists_languages() =>
        _dsl.SuperuserListsLanguagesAsync(CancellationToken.None);

    private Task When_an_unauthenticated_person_attempts_to_list_languages() =>
        _dsl.UnauthenticatedPersonAttemptsToListLanguagesAsync(CancellationToken.None);

    private Task Then_sign_in_is_required() => _dsl.AuthenticationShouldBeRequiredAsync(CancellationToken.None);
    private Task Then_the_language_is_listed() => _dsl.LanguageShouldBeListedAsync();
}
