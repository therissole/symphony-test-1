using AcceptanceTests.Environment;
using Microsoft.Playwright;

namespace AcceptanceTests.Core;

/// <summary>
/// Owns one authenticated browser session for a scenario, leaving feature navigation and selectors to feature drivers.
/// </summary>
internal sealed class BrowserTransport(
    AcceptanceOptions options,
    string? userName = null,
    string? password = null) : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public async Task<IPage> PageAsync()
    {
        // Reusing the page preserves the authenticated session across the scenario's feature steps.
        if (_page is not null) return _page;
        var configuredUserName = userName ?? options.BrowserUserName;
        var configuredPassword = password ?? options.BrowserPassword;
        if (string.IsNullOrWhiteSpace(configuredUserName) || string.IsNullOrWhiteSpace(configuredPassword))
            throw new InvalidOperationException("Browser acceptance tests require browser credentials.");
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _page = await _browser.NewPageAsync();
        var returnUrl = Uri.EscapeDataString(options.BaseUri.ToString());
        // Authenticate through the deployed OIDC flow; browser acceptance tests do not inject a token.
        await _page.GotoAsync(new Uri(options.BaseUri, $"authentication/login?returnUrl={returnUrl}").ToString());
        await _page.Locator("#username").FillAsync(configuredUserName);
        await _page.Locator("#password").FillAsync(configuredPassword);
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
