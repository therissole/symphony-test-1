namespace AcceptanceTests.Environment;

internal sealed record AcceptanceOptions(
    Uri BaseUri,
    string? AccessToken,
    Uri? TokenEndpoint,
    string? ClientId,
    string? ClientSecret,
    string? BrowserUserName,
    string? BrowserPassword,
    string? SuperuserUserName,
    string? SuperuserPassword,
    string? StandardUserName,
    string? StandardUserPassword)
{
    public static bool TryLoad(out AcceptanceOptions? options)
    {
        var baseUrl = System.Environment.GetEnvironmentVariable("ACCEPTANCE_BASE_URL");
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            options = null;
            return false;
        }

        var tokenEndpoint = System.Environment.GetEnvironmentVariable("ACCEPTANCE_TOKEN_ENDPOINT");
        options = new AcceptanceOptions(
            baseUri,
            System.Environment.GetEnvironmentVariable("ACCEPTANCE_ACCESS_TOKEN"),
            Uri.TryCreate(tokenEndpoint, UriKind.Absolute, out var uri) ? uri : null,
            System.Environment.GetEnvironmentVariable("ACCEPTANCE_CLIENT_ID"),
            System.Environment.GetEnvironmentVariable("ACCEPTANCE_CLIENT_SECRET"),
            System.Environment.GetEnvironmentVariable("ACCEPTANCE_BROWSER_USERNAME"),
            System.Environment.GetEnvironmentVariable("ACCEPTANCE_BROWSER_PASSWORD"),
            System.Environment.GetEnvironmentVariable("ACCEPTANCE_SUPERUSER_USERNAME"),
            System.Environment.GetEnvironmentVariable("ACCEPTANCE_SUPERUSER_PASSWORD"),
            System.Environment.GetEnvironmentVariable("ACCEPTANCE_STANDARD_USER_USERNAME"),
            System.Environment.GetEnvironmentVariable("ACCEPTANCE_STANDARD_USER_PASSWORD"));
        return true;
    }
}
