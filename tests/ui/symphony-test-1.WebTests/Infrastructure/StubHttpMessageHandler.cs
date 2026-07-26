using System.Net;
using System.Net.Http.Json;

namespace SymphonyTest1.WebTests.Infrastructure;

internal sealed class StubHttpMessageHandler(
    IReadOnlyDictionary<string, object> responses,
    HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath
            ?? throw new InvalidOperationException("The request URI is required.");

        if (!responses.TryGetValue(path, out var body))
        {
            throw new InvalidOperationException($"No response was configured for {path}.");
        }

        return Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = JsonContent.Create(body),
            RequestMessage = request
        });
    }
}
