using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AcceptanceTests.Environment;

namespace AcceptanceTests.Core;

/// <summary>
/// Provides the protocol-neutral HTTP mechanics used by feature API drivers; feature routes and payloads stay outside Core.
/// </summary>
internal sealed class ApiTransport(
    AcceptanceOptions options,
    string? userName = null,
    string? password = null,
    bool authenticate = true) : IAsyncDisposable
{
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = new() { BaseAddress = options.BaseUri };
    private string? _token = options.AccessToken;

    public async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, HttpStatusCode expected, CancellationToken ct)
    {
        var response = await SendForResponseAsync(method, path, body, ct);
        Ensure(response, expected);
        return string.IsNullOrEmpty(response.Body)
            ? default
            : JsonSerializer.Deserialize<T>(response.Body, Json);
    }

    /// <summary>Returns the public HTTP response so a feature driver can assert an expected failure contract.</summary>
    public async Task<ApiResponse> SendForResponseAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (authenticate)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(ct));
        }

        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        using var response = await _client.SendAsync(request, ct);
        return new ApiResponse(response.StatusCode, await response.Content.ReadAsStringAsync(ct));
    }

    public static ApiTransport Anonymous(AcceptanceOptions options) =>
        new(options, authenticate: false);

    public ValueTask DisposeAsync() { _client.Dispose(); return ValueTask.CompletedTask; }

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        // A scenario reuses one token so its feature driver does not repeatedly call the identity provider.
        if (!string.IsNullOrWhiteSpace(_token)) return _token;
        if (options.TokenEndpoint is null || string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
            throw new InvalidOperationException("Configure acceptance token credentials.");
        using var client = new HttpClient();
        var tokenRequest = new List<KeyValuePair<string, string>>
        {
            new("client_id", options.ClientId),
            new("client_secret", options.ClientSecret)
        };

        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            tokenRequest.Add(new("grant_type", "client_credentials"));
        }
        else
        {
            tokenRequest.Add(new("grant_type", "password"));
            tokenRequest.Add(new("username", userName));
            tokenRequest.Add(new("password", password));
        }

        using var response = await client.PostAsync(options.TokenEndpoint, new FormUrlEncodedContent(tokenRequest), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        Ensure(new ApiResponse(response.StatusCode, body), HttpStatusCode.OK);
        _token = (await response.Content.ReadFromJsonAsync<Token>(Json, ct))?.AccessToken ?? throw new InvalidOperationException("Token response was empty.");
        return _token;
    }

    private static void Ensure(ApiResponse response, HttpStatusCode expected)
    {
        if (response.StatusCode == expected) return;
        // Preserve the public response body in the failure: it is the useful diagnostic at this black-box boundary.
        throw new AssertionException($"Expected HTTP {(int)expected} but received {(int)response.StatusCode}. {response.Body}");
    }

    private sealed record Token([property: JsonPropertyName("access_token")] string AccessToken);
}

internal sealed record ApiResponse(HttpStatusCode StatusCode, string Body);
