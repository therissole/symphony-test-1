using Bunit;
using Bunit.JSInterop;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using SymphonyTest1.Web.Features.Authentication;

namespace SymphonyTest1.WebTests.Features.Authentication;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class SignOutButtonTests : BunitContext
{
    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    [Test]
    public void Click_InitiatesTrustedLogoutNavigation()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        var component = Render<SignOutButton>();

        component.Find("[data-testid='sign-out-button']").Click();

        Assert.Multiple(() =>
        {
            Assert.That(
                navigation.Uri,
                Is.EqualTo("http://localhost/authentication/logout"));
            Assert.That(
                ((BunitNavigationManager)navigation).History.Single().Options.HistoryEntryState,
                Is.Not.Null.And.Not.Empty);
        });
    }
}
