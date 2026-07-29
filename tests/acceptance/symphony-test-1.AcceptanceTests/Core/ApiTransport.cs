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
internal sealed class ApiTransport(AcceptanceOptions options) : IAsyncDisposable
{
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client = new() { BaseAddress = options.BaseUri };
    private string? _token = options.AccessToken;

    public async Task<T?> SendAsync<T>(HttpMethod method, string path, object? body, HttpStatusCode expected, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(ct));
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        using var response = await _client.SendAsync(request, ct);
        await EnsureAsync(response, expected, ct);
        return response.Content.Headers.ContentLength == 0 ? default : await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    public ValueTask DisposeAsync() { _client.Dispose(); return ValueTask.CompletedTask; }

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        // A scenario reuses one token so its feature driver does not repeatedly call the identity provider.
        if (!string.IsNullOrWhiteSpace(_token)) return _token;
        if (options.TokenEndpoint is null || string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
            throw new InvalidOperationException("Configure acceptance token credentials.");
        using var client = new HttpClient();
        using var response = await client.PostAsync(options.TokenEndpoint, new FormUrlEncodedContent([
            new("grant_type", "client_credentials"), new("client_id", options.ClientId), new("client_secret", options.ClientSecret)]), ct);
        await EnsureAsync(response, HttpStatusCode.OK, ct);
        _token = (await response.Content.ReadFromJsonAsync<Token>(Json, ct))?.AccessToken ?? throw new InvalidOperationException("Token response was empty.");
        return _token;
    }

    private static async Task EnsureAsync(HttpResponseMessage response, HttpStatusCode expected, CancellationToken ct)
    {
        if (response.StatusCode == expected) return;
        // Preserve the public response body in the failure: it is the useful diagnostic at this black-box boundary.
        throw new AssertionException($"Expected HTTP {(int)expected} but received {(int)response.StatusCode}. {await response.Content.ReadAsStringAsync(ct)}");
    }

    private sealed record Token([property: JsonPropertyName("access_token")] string AccessToken);
}
