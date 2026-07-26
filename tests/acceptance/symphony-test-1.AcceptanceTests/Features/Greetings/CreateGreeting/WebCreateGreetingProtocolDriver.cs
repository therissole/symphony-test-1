using AcceptanceTests.Core;
using Microsoft.Playwright;

namespace AcceptanceTests.Features.Greetings.CreateGreeting;

internal sealed class WebCreateGreetingProtocolDriver(BrowserTransport browser) : ICreateGreetingProtocolDriver
{
    public async Task<SupportedLanguage> CreateLanguageEntryAsync(string name, string code, CancellationToken ct)
    {
        var page = await browser.PageAsync();
        await page.GotoAsync(new Uri(new Uri(page.Url), "/languages").ToString());
        await page.GetByTestId("add-language").ClickAsync();
        await page.GetByLabel("Name").FillAsync(name); await page.GetByLabel("Code").FillAsync(code);
        await page.GetByTestId("save-language").ClickAsync(); await page.Locator("tr", new() { HasTextString = name }).WaitForAsync();
        return new(null, name, code);
    }
    public async Task CreateGreetingAsync(SupportedLanguage language, string text, bool formal, CancellationToken ct)
    {
        var page = await browser.PageAsync(); await page.GotoAsync(new Uri(new Uri(page.Url), "/greetings").ToString());
        await page.GetByTestId("add-greeting").ClickAsync(); await page.GetByTestId("greeting-language").ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = $"{language.Name} ({language.Code})" }).ClickAsync();
        await page.GetByTestId("greeting-text").FillAsync(text); if (formal) await page.GetByTestId("greeting-formal").ClickAsync();
        await page.GetByTestId("save-greeting").ClickAsync(); await page.Locator("tr", new() { HasTextString = text }).WaitForAsync();
    }
    public async Task<bool> IsVisibleAsync(SupportedLanguage language, IntroducedGreeting greeting, CancellationToken ct)
    {
        var page = await browser.PageAsync(); await page.GotoAsync(new Uri(new Uri(page.Url), "/greetings").ToString());
        var row = page.Locator("tr", new() { HasTextString = greeting.Text }); await row.WaitForAsync();
        return (await row.TextContentAsync())?.Contains(language.Name, StringComparison.Ordinal) == true;
    }
    public async Task DeleteLanguageAsync(SupportedLanguage language, CancellationToken ct)
    {
        var page = await browser.PageAsync(); await page.GotoAsync(new Uri(new Uri(page.Url), "/languages").ToString());
        var row = page.Locator("tr", new() { HasTextString = language.Name }); await row.GetByLabel("Delete language").ClickAsync();
        await page.GetByTestId("confirm-delete-language").ClickAsync(); await row.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
    }
    public ValueTask DisposeAsync() => browser.DisposeAsync();
}
