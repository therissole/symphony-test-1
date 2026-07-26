using AcceptanceTests.Environment;
using Microsoft.Playwright;

namespace AcceptanceTests.Core;

internal sealed class BrowserTransport(AcceptanceOptions options) : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public async Task<IPage> PageAsync()
    {
        if (_page is not null) return _page;
        if (string.IsNullOrWhiteSpace(options.BrowserUserName) || string.IsNullOrWhiteSpace(options.BrowserPassword))
            throw new InvalidOperationException("Browser acceptance tests require browser credentials.");
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _page = await _browser.NewPageAsync();
        var returnUrl = Uri.EscapeDataString(options.BaseUri.ToString());
        await _page.GotoAsync(new Uri(options.BaseUri, $"authentication/login?returnUrl={returnUrl}").ToString());
        await _page.Locator("#username").FillAsync(options.BrowserUserName);
        await _page.Locator("#password").FillAsync(options.BrowserPassword);
        await _page.Locator("#kc-login").ClickAsync();
        await _page.GetByTestId("dashboard").WaitForAsync();
        return _page;
    }

    public async ValueTask DisposeAsync()
    {
        if (_page is not null) await _page.CloseAsync();
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}
