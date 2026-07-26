using Microsoft.Playwright;
using SymphonyTest1.IntegrationTests.Infrastructure;

namespace SymphonyTest1.E2ETests.Features;

[TestFixture]
[NonParallelizable]
public sealed class AdministrationUiWorkflowTests
{
    private IntegrationTestWebAppFactory _factory = null!;
    private HttpClient _hostClient = null!;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IPage _page = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new IntegrationTestWebAppFactory();
        _factory.UseKestrel(0);
        await _factory.StartAsync();

        // Creating the client starts Kestrel and exposes its dynamically assigned address.
        _hostClient = _factory.CreateClient();
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        _page = await _browser.NewPageAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _page.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
        _hostClient.Dispose();
        await _factory.StopAsync();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task CatalogCrudWorkflow_CompletesThroughTheBrowser()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var originalLanguageName = $"UI Language {suffix}";
        var updatedLanguageName = $"Updated Language {suffix}";
        var languageCode = $"ui{suffix}";
        var originalGreeting = $"Hello from {suffix}";
        var updatedGreeting = $"Welcome from {suffix}";

        await _page.GotoAsync(_hostClient.BaseAddress!.ToString());
        await _page.GetByTestId("dashboard").WaitForAsync();
        await ExpectVisibleAsync(_page.GetByTestId("profile-placeholder"));

        await _page.GetByRole(AriaRole.Link, new() { Name = "Languages", Exact = true }).ClickAsync();
        await _page.GetByTestId("languages-grid").WaitForAsync();
        await AssertMainContentClearsAppBarAsync();
        await _page.GetByTestId("add-language").ClickAsync();
        await _page.GetByLabel("Name").FillAsync(originalLanguageName);
        await _page.GetByLabel("Code").FillAsync(languageCode);
        await _page.GetByTestId("save-language").ClickAsync();

        var languageRow = _page.Locator("tr", new() { HasTextString = originalLanguageName });
        await languageRow.WaitForAsync();
        await languageRow.GetByLabel("View language").ClickAsync();
        await _page.GetByTestId("language-details").WaitForAsync();
        Assert.That(await _page.GetByTestId("language-details").TextContentAsync(),
            Does.Contain(languageCode));
        await _page.GetByTestId("close-language-details").ClickAsync();

        await languageRow.GetByLabel("Edit language").ClickAsync();
        await _page.GetByLabel("Name").FillAsync(updatedLanguageName);
        await _page.GetByTestId("save-language").ClickAsync();
        languageRow = _page.Locator("tr", new() { HasTextString = updatedLanguageName });
        await languageRow.WaitForAsync();

        await _page.GetByRole(AriaRole.Link, new() { Name = "Greetings", Exact = true }).ClickAsync();
        await _page.GetByTestId("greetings-grid").WaitForAsync();
        await _page.GetByTestId("add-greeting").ClickAsync();
        await _page.GetByTestId("greeting-language").ClickAsync();
        await _page.GetByRole(AriaRole.Option, new() { Name = $"{updatedLanguageName} ({languageCode})" })
            .ClickAsync();
        await _page.GetByTestId("greeting-text").FillAsync(originalGreeting);
        await _page.GetByTestId("greeting-formal").ClickAsync();
        await _page.GetByTestId("save-greeting").ClickAsync();

        var greetingRow = _page.Locator("tr", new() { HasTextString = originalGreeting });
        await greetingRow.WaitForAsync();
        await greetingRow.GetByLabel("View greeting").ClickAsync();
        await _page.GetByTestId("greeting-details").WaitForAsync();
        Assert.That(await _page.GetByTestId("greeting-details").TextContentAsync(),
            Does.Contain(updatedLanguageName));
        await _page.GetByTestId("close-greeting-details").ClickAsync();

        await greetingRow.GetByLabel("Edit greeting").ClickAsync();
        await _page.GetByTestId("greeting-text").FillAsync(updatedGreeting);
        await _page.GetByTestId("save-greeting").ClickAsync();
        greetingRow = _page.Locator("tr", new() { HasTextString = updatedGreeting });
        await greetingRow.WaitForAsync();

        await greetingRow.GetByLabel("Delete greeting").ClickAsync();
        await _page.GetByTestId("confirm-delete-greeting").ClickAsync();
        await greetingRow.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });

        await _page.GetByRole(AriaRole.Link, new() { Name = "Languages", Exact = true }).ClickAsync();
        languageRow = _page.Locator("tr", new() { HasTextString = updatedLanguageName });
        await languageRow.GetByLabel("Delete language").ClickAsync();
        await _page.GetByTestId("confirm-delete-language").ClickAsync();
        await languageRow.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
    }

    private static async Task ExpectVisibleAsync(ILocator locator)
    {
        await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        Assert.That(await locator.IsVisibleAsync(), Is.True);
    }

    private async Task AssertMainContentClearsAppBarAsync()
    {
        var appBar = await _page.Locator("header.app-bar").BoundingBoxAsync();
        var heading = await _page.Locator("h1").BoundingBoxAsync();

        Assert.Multiple(() =>
        {
            Assert.That(appBar, Is.Not.Null);
            Assert.That(heading, Is.Not.Null);
        });
        Assert.That(heading!.Y, Is.GreaterThanOrEqualTo(appBar!.Y + appBar.Height + 20));
    }
}
