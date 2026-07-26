using System.Net;
using Bunit;
using Bunit.JSInterop;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using SymphonyTest1.Web.Features.Dashboard;
using SymphonyTest1.WebTests.Infrastructure;

namespace SymphonyTest1.WebTests.Features.Dashboard;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class DashboardPageTests : BunitContext
{
    [SetUp]
    public void SetUp()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    [Test]
    public void Dashboard_RendersCatalogCountsAndRecentActivity()
    {
        var languageId = Guid.NewGuid();
        RegisterHttpClient(new Dictionary<string, object>
        {
            ["/api/languages/"] = new[]
            {
                new
                {
                    Id = languageId,
                    Name = "English",
                    Code = "en",
                    CreatedAt = new DateTimeOffset(2026, 7, 24, 9, 0, 0, TimeSpan.Zero),
                    UpdatedAt = new DateTimeOffset(2026, 7, 25, 9, 0, 0, TimeSpan.Zero)
                }
            },
            ["/api/greetings/"] = new[]
            {
                new
                {
                    Id = Guid.NewGuid(),
                    LanguageId = languageId,
                    GreetingText = "Good morning",
                    Formal = true,
                    CreatedAt = new DateTimeOffset(2026, 7, 25, 9, 0, 0, TimeSpan.Zero),
                    UpdatedAt = new DateTimeOffset(2026, 7, 26, 9, 0, 0, TimeSpan.Zero)
                }
            }
        });

        var component = Render<DashboardPage>();

        component.WaitForAssertion(() =>
        {
            Assert.That(component.Find("[data-testid='language-count']").TextContent, Is.EqualTo("1"));
            Assert.That(component.Find("[data-testid='greeting-count']").TextContent, Is.EqualTo("1"));
            Assert.That(component.Markup, Does.Contain("Good morning"));
            Assert.That(component.Markup, Does.Contain("English"));
        });
    }

    [Test]
    public void Dashboard_WhenApiFails_RendersRecoverableErrorState()
    {
        RegisterHttpClient(
            new Dictionary<string, object>
            {
                ["/api/languages/"] = Array.Empty<object>(),
                ["/api/greetings/"] = Array.Empty<object>()
            },
            HttpStatusCode.ServiceUnavailable);

        var component = Render<DashboardPage>();

        component.WaitForAssertion(() =>
        {
            Assert.That(component.Find("[data-testid='error-panel']").TextContent,
                Does.Contain("catalog summary is unavailable"));
            Assert.That(component.Markup, Does.Contain("Try again"));
        });
    }

    private void RegisterHttpClient(
        IReadOnlyDictionary<string, object> responses,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        Services.AddSingleton(new HttpClient(new StubHttpMessageHandler(responses, statusCode))
        {
            BaseAddress = new Uri("http://localhost/")
        });
    }
}
