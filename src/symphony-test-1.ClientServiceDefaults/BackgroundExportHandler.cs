using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Polly;

namespace SymphonyTest1.ClientServiceDefaults;

// The OpenTelemetry exporter performs synchronous-over-asynchronous work. On single-threaded
// WebAssembly that can deadlock, so acknowledge the exporter immediately and send a safe copy of
// the request in the background.
internal sealed class BackgroundExportHandler(
    ResiliencePipeline<HttpResponseMessage> pipeline,
    ILogger logger) : DelegatingHandler(new HttpClientHandler())
{
    private static readonly Action<ILogger, Uri, HttpStatusCode, Exception?>
        ExportCompletedWithFailure = LoggerMessage.Define<Uri, HttpStatusCode>(
            LogLevel.Warning,
            new EventId(1, nameof(ExportCompletedWithFailure)),
            "OTLP export to {Uri} completed with status {StatusCode} after retries.");

    private static readonly Action<ILogger, Uri, Exception?>
        ExportFailed = LoggerMessage.Define<Uri>(
            LogLevel.Warning,
            new EventId(2, nameof(ExportFailed)),
            "OTLP export to {Uri} failed after retries.");

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var snapshot = RequestSnapshot.Capture(request);
        _ = SendWithRetryAsync(snapshot);

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    private async Task SendWithRetryAsync(RequestSnapshot snapshot)
    {
        try
        {
            var response = await pipeline.ExecuteAsync(async cancellationToken =>
            {
                using var request = snapshot.CreateRequest();
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }, CancellationToken.None).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                ExportCompletedWithFailure(
                    logger,
                    snapshot.RequestUri,
                    response.StatusCode,
                    null);
            }

            response.Dispose();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ExportFailed(logger, snapshot.RequestUri, exception);
        }
    }
}

internal sealed class RequestSnapshot
{
    public required HttpMethod Method { get; init; }

    public required Uri RequestUri { get; init; }

    public required List<KeyValuePair<string, IEnumerable<string>>> Headers { get; init; }

    public byte[]? ContentBytes { get; init; }

    public MediaTypeHeaderValue? ContentType { get; init; }

    public static RequestSnapshot Capture(HttpRequestMessage request)
    {
        var contentBytes = request.Content?
            .ReadAsByteArrayAsync()
            .GetAwaiter()
            .GetResult();

        return new RequestSnapshot
        {
            Method = request.Method,
            RequestUri = request.RequestUri
                ?? throw new InvalidOperationException("OTLP export request has no URI."),
            Headers = request.Headers
                .Select(header =>
                    new KeyValuePair<string, IEnumerable<string>>(
                        header.Key,
                        header.Value.ToArray()))
                .ToList(),
            ContentBytes = contentBytes,
            ContentType = request.Content?.Headers.ContentType
        };
    }

    public HttpRequestMessage CreateRequest()
    {
        var request = new HttpRequestMessage(Method, RequestUri);

        foreach (var header in Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (ContentBytes is not null)
        {
            request.Content = new ByteArrayContent(ContentBytes);
            request.Content.Headers.ContentType = ContentType;
        }

        return request;
    }
}
