using System.Net.Http.Json;
using System.Text.Json;

namespace SymphonyTest1.Web.Infrastructure;

internal sealed record ApiProblem(
    string? Title,
    string? Detail,
    int? Status,
    Dictionary<string, string[]>? Errors)
{
    public string Message => Detail ?? Title ?? "The request could not be completed.";

    public string? GetFieldError(string propertyName) =>
        Errors?.GetValueOrDefault(propertyName)?.FirstOrDefault();
}

internal static class ApiProblemReader
{
    public static async Task<ApiProblem> ReadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken)
                ?? CreateFallback(response);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // Preserve a useful message if a proxy returns an HTML or empty error response.
            return CreateFallback(response);
        }
    }

    private static ApiProblem CreateFallback(HttpResponseMessage response) =>
        new(
            "Request failed",
            $"The server returned status code {(int)response.StatusCode}.",
            (int)response.StatusCode,
            null);
}
