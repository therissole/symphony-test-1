using AcceptanceTests.Core;
using AcceptanceTests.Features.Greetings.Dsl;
using Microsoft.Playwright;

namespace AcceptanceTests.Features.Greetings.ProtocolDrivers;

internal sealed class GreetingsWebProtocolDriver(BrowserTransport browser, Uri? publicBaseUri = null)
    : ICreateGreetingAuthorizationProtocolDriver,
      IDeleteGreetingAuthorizationProtocolDriver,
      IListGreetingsAuthorizationProtocolDriver,
      IGetGreetingAuthorizationProtocolDriver,
      IUpdateGreetingAuthorizationProtocolDriver,
      ICreateGreetingAuthenticationProtocolDriver,
      IListGreetingsAuthenticationProtocolDriver,
      IGetGreetingAuthenticationProtocolDriver,
      IUpdateGreetingAuthenticationProtocolDriver,
      IDeleteGreetingAuthenticationProtocolDriver
{
    private const string CreateGreetingDeniedMessage = "You do not have permission to create a greeting.";
    private const string DeleteGreetingDeniedMessage = "You do not have permission to delete this greeting.";
    private const string UpdateGreetingDeniedMessage = "You do not have permission to update this greeting.";
    private IPage? _createGreetingPage;
    private IPage? _deleteGreetingPage;
    private IPage? _updateGreetingPage;
    private IPage? _unauthenticatedCreatePage;
    private IPage? _unauthenticatedListPage;
    private IPage? _unauthenticatedGetPage;
    private IPage? _unauthenticatedUpdatePage;
    private IPage? _unauthenticatedDeletePage;
    public async Task<SupportedLanguage> CreateLanguageEntryAsync(string name, string code, CancellationToken ct)
    {
        var page = await browser.PageAsync();
        await page.GotoAsync(new Uri(new Uri(page.Url), "/languages").ToString());
        await page.GetByTestId("add-language").ClickAsync();
        await page.GetByLabel("Name").FillAsync(name);
        await page.GetByLabel("Code").FillAsync(code);
        await page.GetByTestId("save-language").ClickAsync();
        await page.Locator("tr", new() { HasTextString = name }).WaitForAsync();
        return new SupportedLanguage(null, name, code);
    }

    public async Task CreateGreetingAsync(SupportedLanguage language, string text, bool formal, CancellationToken ct)
    {
        var page = await OpenCreateGreetingAsync(language, text, formal);
        await page.GetByTestId("save-greeting").ClickAsync();
        await GreetingRow(page, language, text).WaitForAsync();
    }

    public async Task AttemptToCreateGreetingAsync(
        SupportedLanguage language,
        string text,
        bool formal,
        CancellationToken ct)
    {
        _createGreetingPage = await OpenCreateGreetingAsync(language, text, formal);
        await _createGreetingPage.GetByTestId("save-greeting").ClickAsync();
    }

    public async Task CreationShouldBeDeniedAsync(CancellationToken ct)
    {
        var page = _createGreetingPage
            ?? throw new AssertionException("Greeting creation has not been attempted.");
        await page.GetByText(CreateGreetingDeniedMessage, new() { Exact = true }).WaitForAsync();
    }

    public async Task<bool> IsVisibleAsync(SupportedLanguage language, IntroducedGreeting greeting, CancellationToken ct)
    {
        var page = await browser.PageAsync();
        await page.GotoAsync(new Uri(new Uri(page.Url), "/greetings").ToString());
        await page.GetByTestId("greetings-grid").WaitForAsync();
        var row = GreetingRow(page, language, greeting.Text);
        return await row.CountAsync() > 0
            && (await row.TextContentAsync())?.Contains(language.Name, StringComparison.Ordinal) == true;
    }

    public async Task<ManagedGreeting> CreateGreetingForDeletionAsync(
        SupportedLanguage language,
        string text,
        bool formal,
        CancellationToken ct) =>
        await CreateManagedGreetingAsync(language, text, formal, ct);

    public async Task<ManagedGreeting> CreateManagedGreetingAsync(
        SupportedLanguage language,
        string text,
        bool formal,
        CancellationToken ct)
    {
        await CreateGreetingAsync(language, text, formal, ct);
        var page = await browser.PageAsync();
        var row = GreetingRow(page, language, text);
        var testId = await row.GetByLabel("View greeting").GetAttributeAsync("data-testid");
        const string viewGreetingPrefix = "view-greeting-";
        var id = testId?.StartsWith(viewGreetingPrefix, StringComparison.Ordinal) == true
            && Guid.TryParse(testId[viewGreetingPrefix.Length..], out var parsed)
            ? parsed
            : (Guid?)null;
        return new ManagedGreeting(id, text, formal);
    }

    public async Task DeleteGreetingAsync(ManagedGreeting greeting, CancellationToken ct)
    {
        var page = await DeleteGreetingFromListAsync(greeting);
        await page.GetByTestId($"view-greeting-{RequiredId(greeting)}")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
    }

    public async Task AttemptToDeleteGreetingAsync(ManagedGreeting greeting, CancellationToken ct) =>
        _deleteGreetingPage = await DeleteGreetingFromListAsync(greeting);

    public async Task DeletionShouldBeDeniedAsync(CancellationToken ct)
    {
        var page = _deleteGreetingPage ?? throw new AssertionException("The deletion has not been attempted.");
        await page.GetByText(DeleteGreetingDeniedMessage, new() { Exact = true }).WaitForAsync();
    }

    public async Task<bool> IsGreetingVisibleAsync(SupportedLanguage language, ManagedGreeting greeting, CancellationToken ct)
    {
        var page = await browser.PageAsync();
        await page.GotoAsync(new Uri(new Uri(page.Url), "/greetings").ToString());
        await page.GetByTestId("greetings-grid").WaitForAsync();
        return greeting.Id is Guid id
            ? await page.GetByTestId($"view-greeting-{id}").CountAsync() > 0
            : await page.Locator("tr", new() { HasTextString = greeting.Text }).CountAsync() > 0;
    }

    public async Task DeleteLanguageAsync(SupportedLanguage language, CancellationToken ct)
    {
        var page = await browser.PageAsync();
        await page.GotoAsync(new Uri(new Uri(page.Url), "/languages").ToString());
        var row = page.Locator("tr", new() { HasTextString = language.Name });
        await row.GetByLabel("Delete language").ClickAsync();
        await page.GetByTestId("confirm-delete-language").ClickAsync();
        await row.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
    }

    public async Task<IReadOnlyList<ObservedGreeting>> ListGreetingsAsync(CancellationToken ct)
    {
        var page = await OpenGreetingListAsync();
        var rows = page.Locator("tbody tr");
        var count = await rows.CountAsync();
        var greetings = new List<ObservedGreeting>();
        for (var index = 0; index < count; index++)
        {
            var cells = rows.Nth(index).Locator("td");
            if (await cells.CountAsync() >= 3)
            {
                var text = (await cells.Nth(0).TextContentAsync())?.Trim() ?? string.Empty;
                var style = (await cells.Nth(2).TextContentAsync())?.Trim();
                greetings.Add(new ObservedGreeting(
                    text,
                    style == "Formal"));
            }
        }

        return greetings;
    }

    public async Task<ObservedGreeting> GetGreetingAsync(ManagedGreeting greeting, CancellationToken ct)
    {
        var page = await OpenGreetingListAsync();
        await page.GetByTestId($"view-greeting-{RequiredId(greeting)}").ClickAsync();
        var details = page.GetByTestId("greeting-details");
        await details.WaitForAsync();
        var values = details.Locator("dd");
        var text = (await values.Nth(0).TextContentAsync())?.Trim()
            ?? throw new AssertionException("The greeting text was not shown.");
        var style = (await values.Nth(2).TextContentAsync())?.Trim();
        return new ObservedGreeting(text, style == "Formal");
    }

    public async Task UpdateGreetingAsync(
        ManagedGreeting greeting,
        SupportedLanguage language,
        string text,
        bool formal,
        CancellationToken ct)
    {
        var page = await OpenUpdateGreetingAsync(greeting, text, formal);
        await page.GetByTestId("save-greeting").ClickAsync();
        await page.GetByTestId("greeting-text")
            .WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        var updatedRow = page.Locator(
            $"tr:has([data-testid='view-greeting-{RequiredId(greeting)}'])",
            new() { HasTextString = text });
        await updatedRow.WaitForAsync();
    }

    public async Task AttemptToUpdateGreetingAsync(
        ManagedGreeting greeting,
        SupportedLanguage language,
        string text,
        bool formal,
        CancellationToken ct)
    {
        _updateGreetingPage = await OpenUpdateGreetingAsync(greeting, text, formal);
        await _updateGreetingPage.GetByTestId("save-greeting").ClickAsync();
    }

    public async Task UpdateShouldBeDeniedAsync(CancellationToken ct)
    {
        var page = _updateGreetingPage
            ?? throw new AssertionException("Greeting update has not been attempted.");
        await page.GetByText(UpdateGreetingDeniedMessage, new() { Exact = true }).WaitForAsync();
    }

    public Task<ObservedGreeting> GetGreetingStateAsync(ManagedGreeting greeting, CancellationToken ct) =>
        GetGreetingAsync(greeting, ct);

    public async Task AttemptToCreateGreetingWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedCreatePage = await NavigateToProtectedGreetingsAsync();

    public Task AuthenticationShouldBeRequiredAndCreationUnavailableAsync(CancellationToken ct) =>
        AssertSignInRequiredAsync(
            _unauthenticatedCreatePage,
            page => page.GetByTestId("add-greeting"),
            "Greeting creation");

    public async Task AttemptToListGreetingsWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedListPage = await NavigateToProtectedGreetingsAsync();

    public Task AuthenticationShouldBeRequiredAndListUnavailableAsync(CancellationToken ct) =>
        AssertSignInRequiredAsync(
            _unauthenticatedListPage,
            page => page.GetByTestId("greetings-grid"),
            "Greeting list");

    public async Task AttemptToGetGreetingWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedGetPage = await NavigateToProtectedGreetingsAsync();

    public Task AuthenticationShouldBeRequiredAndDetailsUnavailableAsync(CancellationToken ct) =>
        AssertSignInRequiredAsync(
            _unauthenticatedGetPage,
            page => page.GetByTestId("greeting-details"),
            "Greeting details");

    public async Task AttemptToUpdateGreetingWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedUpdatePage = await NavigateToProtectedGreetingsAsync();

    public Task AuthenticationShouldBeRequiredAndUpdateUnavailableAsync(CancellationToken ct) =>
        AssertSignInRequiredAsync(
            _unauthenticatedUpdatePage,
            page => page.Locator("[data-testid^='edit-greeting-']"),
            "Greeting update");

    public async Task AttemptToDeleteGreetingWithoutAuthenticationAsync(CancellationToken ct) =>
        _unauthenticatedDeletePage = await NavigateToProtectedGreetingsAsync();

    public Task AuthenticationShouldBeRequiredAndDeletionUnavailableAsync(CancellationToken ct) =>
        AssertSignInRequiredAsync(
            _unauthenticatedDeletePage,
            page => page.Locator("[data-testid^='delete-greeting-']"),
            "Greeting deletion");

    public ValueTask DisposeAsync() => browser.DisposeAsync();

    private async Task<IPage> OpenCreateGreetingAsync(
        SupportedLanguage language,
        string text,
        bool formal)
    {
        var page = await OpenGreetingListAsync();
        await page.GetByTestId("add-greeting").ClickAsync();
        await page.GetByTestId("greeting-language").ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = $"{language.Name} ({language.Code})" }).ClickAsync();
        await page.GetByTestId("greeting-text").FillAsync(text);
        await SetFormalAsync(page, formal);
        return page;
    }

    private async Task<IPage> OpenUpdateGreetingAsync(ManagedGreeting greeting, string text, bool formal)
    {
        var page = await OpenGreetingListAsync();
        await page.GetByTestId($"edit-greeting-{RequiredId(greeting)}").ClickAsync();
        await page.GetByTestId("greeting-text").FillAsync(text);
        await SetFormalAsync(page, formal);
        return page;
    }

    private async Task<IPage> OpenGreetingListAsync()
    {
        var page = await browser.PageAsync();
        await page.GotoAsync(new Uri(new Uri(page.Url), "/greetings").ToString());
        await page.GetByTestId("greetings-grid").WaitForAsync();
        return page;
    }

    private async Task<IPage> NavigateToProtectedGreetingsAsync()
    {
        var page = await browser.PageAsync();
        var baseUri = publicBaseUri
            ?? (Uri.TryCreate(page.Url, UriKind.Absolute, out var currentUri)
                ? currentUri
                : throw new AssertionException("The public application address is unavailable."));
        await page.GotoAsync(new Uri(baseUri, "/greetings").ToString());
        return page;
    }

    private static async Task AssertSignInRequiredAsync(
        IPage? page,
        Func<IPage, ILocator> protectedContent,
        string request)
    {
        var attemptedPage = page
            ?? throw new AssertionException($"{request} has not been attempted.");
        await attemptedPage.Locator("#kc-login").WaitForAsync();
        var protectedContentCount = await protectedContent(attemptedPage).CountAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                attemptedPage.Url,
                Does.Contain("/protocol/openid-connect/auth"),
                $"{request} must redirect to the deployed identity provider.");
            Assert.That(
                protectedContentCount,
                Is.Zero,
                $"{request} controls must not be available before sign-in.");
        });
    }

    private static async Task SetFormalAsync(IPage page, bool formal)
    {
        var formalSwitch = page.GetByTestId("greeting-formal");
        if (await formalSwitch.IsCheckedAsync() != formal)
        {
            await formalSwitch.ClickAsync();
        }
    }

    private async Task<IPage> DeleteGreetingFromListAsync(ManagedGreeting greeting)
    {
        var page = await browser.PageAsync();
        await page.GotoAsync(new Uri(new Uri(page.Url), "/greetings").ToString());
        await page.GetByTestId($"delete-greeting-{RequiredId(greeting)}").ClickAsync();
        await page.GetByTestId("confirm-delete-greeting").ClickAsync();
        return page;
    }

    private static Guid RequiredId(ManagedGreeting greeting) =>
        greeting.Id ?? throw new AssertionException("A greeting identifier is required.");

    private static ILocator GreetingRow(IPage page, SupportedLanguage language, string text) =>
        page.Locator("tr", new() { HasTextString = text })
            .Filter(new LocatorFilterOptions { HasTextString = language.Name });
}
