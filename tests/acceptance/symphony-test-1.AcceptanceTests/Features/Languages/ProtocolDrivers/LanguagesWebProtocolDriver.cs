using AcceptanceTests.Core;
using AcceptanceTests.Features.Languages.Dsl;
using Microsoft.Playwright;

namespace AcceptanceTests.Features.Languages.ProtocolDrivers;

internal sealed class LanguagesWebProtocolDriver(
    BrowserTransport browser,
    Uri? applicationBaseUri = null)
    : ICreateLanguageProtocolDriver,
      IListLanguagesProtocolDriver,
      IGetLanguageProtocolDriver,
      IUpdateLanguageProtocolDriver,
      IDeleteLanguageProtocolDriver
{
    private const string CreateDeniedMessage = "You do not have permission to create a language.";
    private const string UpdateDeniedMessage = "You do not have permission to update this language.";
    private const string DeleteDeniedMessage = "You do not have permission to delete this language.";
    private IPage? _creationAttemptPage;
    private IPage? _updateAttemptPage;
    private IPage? _deletionAttemptPage;
    private IPage? _unauthenticatedAttemptPage;

    public async Task<ManagedLanguage> CreateLanguageAsync(string name, string code, CancellationToken ct)
    {
        var language = new ManagedLanguage(null, name, code);
        var page = await LanguagesPageAsync();
        await OpenCreateDialogAsync(page);
        await FillLanguageAsync(page, language);
        await page.GetByTestId("save-language").ClickAsync();
        await LanguageRow(page, language).WaitForAsync();
        return language;
    }

    public async Task AttemptToCreateLanguageAsync(ManagedLanguage language, CancellationToken ct)
    {
        _creationAttemptPage = await LanguagesPageAsync();
        await OpenCreateDialogAsync(_creationAttemptPage);
        await FillLanguageAsync(_creationAttemptPage, language);
        await _creationAttemptPage.GetByTestId("save-language").ClickAsync();
    }

    public Task CreationShouldBeDeniedAsync(CancellationToken ct) =>
        AssertDeniedAsync(_creationAttemptPage, CreateDeniedMessage, "Language creation");

    public Task AttemptToCreateLanguageWithoutAuthenticationAsync(CancellationToken ct) =>
        AttemptToOpenProtectedLanguagesPageAsync();

    public Task CreationShouldRequireAuthenticationAsync(CancellationToken ct) =>
        AssertAuthenticationRequiredAsync(_unauthenticatedAttemptPage, "add-language", "Language creation");

    public async Task<bool> IsLanguageVisibleAsync(ManagedLanguage language, CancellationToken ct)
    {
        var page = await LanguagesPageAsync();
        return await LanguageRow(page, language).CountAsync() > 0;
    }

    public Task<bool> IsLanguageListedAsync(ManagedLanguage language, CancellationToken ct) =>
        IsLanguageVisibleAsync(language, ct);

    public Task AttemptToListLanguagesWithoutAuthenticationAsync(CancellationToken ct) =>
        AttemptToOpenProtectedLanguagesPageAsync();

    public Task ListingShouldRequireAuthenticationAsync(CancellationToken ct) =>
        AssertAuthenticationRequiredAsync(_unauthenticatedAttemptPage, "languages-grid", "Language listing");

    public async Task<bool> CanViewLanguageDetailsAsync(ManagedLanguage language, CancellationToken ct)
    {
        var page = await LanguagesPageAsync();
        await LanguageRow(page, language).GetByLabel("View language").ClickAsync();
        var details = page.GetByTestId("language-details");
        await details.WaitForAsync();
        var text = await details.TextContentAsync();
        return text?.Contains(language.Name, StringComparison.Ordinal) == true
            && text.Contains(language.Code, StringComparison.Ordinal);
    }

    public Task AttemptToViewLanguageWithoutAuthenticationAsync(CancellationToken ct) =>
        AttemptToOpenProtectedLanguagesPageAsync();

    public Task ViewingShouldRequireAuthenticationAsync(CancellationToken ct) =>
        AssertAuthenticationRequiredAsync(_unauthenticatedAttemptPage, "language-details", "Viewing a language");

    public async Task UpdateLanguageAsync(
        ManagedLanguage language,
        ManagedLanguage update,
        CancellationToken ct)
    {
        var page = await OpenUpdateDialogAsync(language);
        await FillLanguageAsync(page, update);
        await page.GetByTestId("save-language").ClickAsync();
        await LanguageRow(page, update).WaitForAsync();
    }

    public async Task AttemptToUpdateLanguageAsync(
        ManagedLanguage language,
        ManagedLanguage update,
        CancellationToken ct)
    {
        _updateAttemptPage = await OpenUpdateDialogAsync(language);
        await FillLanguageAsync(_updateAttemptPage, update);
        await _updateAttemptPage.GetByTestId("save-language").ClickAsync();
    }

    public Task UpdateShouldBeDeniedAsync(CancellationToken ct) =>
        AssertDeniedAsync(_updateAttemptPage, UpdateDeniedMessage, "Language update");

    public Task AttemptToUpdateLanguageWithoutAuthenticationAsync(CancellationToken ct) =>
        AttemptToOpenProtectedLanguagesPageAsync();

    public Task UpdateShouldRequireAuthenticationAsync(CancellationToken ct) =>
        AssertAuthenticationRequiredAsync(_unauthenticatedAttemptPage, "save-language", "Language update");

    public async Task<bool> LanguageMatchesAsync(
        ManagedLanguage language,
        ManagedLanguage expected,
        CancellationToken ct)
    {
        var page = await LanguagesPageAsync();
        var row = LanguageRow(page, expected);
        return await row.CountAsync() > 0;
    }

    public async Task DeleteLanguageAsync(ManagedLanguage language, CancellationToken ct)
    {
        var page = await LanguagesPageAsync();
        var row = LanguageRow(page, language);
        await row.GetByLabel("Delete language").ClickAsync();
        await page.GetByTestId("confirm-delete-language").ClickAsync();
        await row.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
    }

    public async Task CleanupLanguageAsync(ManagedLanguage language, CancellationToken ct)
    {
        if (await IsLanguageVisibleAsync(language, ct))
        {
            await DeleteLanguageAsync(language, ct);
        }
    }

    public async Task AttemptToDeleteLanguageAsync(ManagedLanguage language, CancellationToken ct)
    {
        _deletionAttemptPage = await LanguagesPageAsync();
        await LanguageRow(_deletionAttemptPage, language).GetByLabel("Delete language").ClickAsync();
        await _deletionAttemptPage.GetByTestId("confirm-delete-language").ClickAsync();
    }

    public Task DeletionShouldBeDeniedAsync(CancellationToken ct) =>
        AssertDeniedAsync(_deletionAttemptPage, DeleteDeniedMessage, "Language deletion");

    public Task AttemptToDeleteLanguageWithoutAuthenticationAsync(CancellationToken ct) =>
        AttemptToOpenProtectedLanguagesPageAsync();

    public Task DeletionShouldRequireAuthenticationAsync(CancellationToken ct) =>
        AssertAuthenticationRequiredAsync(
            _unauthenticatedAttemptPage,
            "confirm-delete-language",
            "Language deletion");

    public ValueTask DisposeAsync() => browser.DisposeAsync();

    private async Task<IPage> LanguagesPageAsync()
    {
        var page = await browser.PageAsync();
        await page.GotoAsync(new Uri(new Uri(page.Url), "/languages").ToString());
        await page.GetByTestId("languages-grid").WaitForAsync();
        return page;
    }

    private async Task AttemptToOpenProtectedLanguagesPageAsync()
    {
        _unauthenticatedAttemptPage = await browser.PageAsync();
        await _unauthenticatedAttemptPage.GotoAsync(
            new Uri(
                applicationBaseUri ??
                    throw new InvalidOperationException(
                        "Anonymous browser requests require the application base URI."),
                "/languages").ToString());
        await _unauthenticatedAttemptPage.Locator("#kc-login").WaitForAsync();
    }

    private static async Task OpenCreateDialogAsync(IPage page)
    {
        await page.GetByTestId("add-language").ClickAsync();
        await page.GetByTestId("language-name").WaitForAsync();
    }

    private async Task<IPage> OpenUpdateDialogAsync(ManagedLanguage language)
    {
        var page = await LanguagesPageAsync();
        await LanguageRow(page, language).GetByLabel("Edit language").ClickAsync();
        await page.GetByTestId("language-name").WaitForAsync();
        return page;
    }

    private static async Task FillLanguageAsync(IPage page, ManagedLanguage language)
    {
        await page.GetByTestId("language-name").FillAsync(language.Name);
        await page.GetByTestId("language-code").FillAsync(language.Code);
    }

    private static async Task AssertDeniedAsync(IPage? page, string message, string action)
    {
        var attemptedPage = page ?? throw new AssertionException($"{action} has not been attempted.");
        await attemptedPage.GetByText(message, new() { Exact = true }).WaitForAsync();
    }

    private static async Task AssertAuthenticationRequiredAsync(
        IPage? page,
        string protectedContentTestId,
        string action)
    {
        var rejectedPage = page ??
            throw new AssertionException($"{action} has not been attempted without authentication.");
        await rejectedPage.Locator("#username").WaitForAsync();
        var protectedContentCount = await rejectedPage.GetByTestId(protectedContentTestId).CountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(
                new Uri(rejectedPage.Url).AbsolutePath,
                Does.Contain("/protocol/openid-connect/auth"),
                "The browser should be on the deployed identity-provider sign-in page.");
            Assert.That(
                protectedContentCount,
                Is.Zero,
                "Protected language content should not be available.");
        });
    }

    private static ILocator LanguageRow(IPage page, ManagedLanguage language) =>
        page.Locator("tr", new() { HasTextString = language.Name });
}
