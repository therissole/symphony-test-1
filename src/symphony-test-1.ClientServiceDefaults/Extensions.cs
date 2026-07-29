using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;

namespace SymphonyTest1.ClientServiceDefaults;

public static class Extensions
{
    public static WebAssemblyHostBuilder AddBlazorClientServiceDefaults(
        this WebAssemblyHostBuilder builder)
    {
        ComponentsMetricsServiceCollectionExtensions.AddComponentsMetrics(builder.Services);
        ComponentsMetricsServiceCollectionExtensions.AddComponentsTracing(builder.Services);

        ConfigureBlazorClientOpenTelemetry(builder);

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddServiceDiscovery();
        });

        return builder;
    }

    private static void ConfigureBlazorClientOpenTelemetry(WebAssemblyHostBuilder builder)
    {
        var otlpPathBase = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(otlpPathBase))
        {
            return;
        }

        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"]
            ?? throw new InvalidOperationException(
                "OTEL_SERVICE_NAME is required when browser telemetry is enabled.");

        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new HttpRetryStrategyOptions
            {
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(5),
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldRetryAfterHeader = true
            })
            .Build();

        var pageOrigin = new Uri(builder.HostEnvironment.BaseAddress);
        var otlpEndpoint = new Uri(pageOrigin, $"{otlpPathBase.TrimEnd('/')}/");

        builder.Services.AddSingleton<IPostConfigureOptions<OtlpExporterOptions>>(services =>
        {
            var logger = services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Aspire.OtlpExport");

            return new PostConfigureOptions<OtlpExporterOptions>(null, options =>
            {
                options.HttpClientFactory = () =>
                    new HttpClient(new BackgroundExportHandler(pipeline, logger));
            });
        });

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.SetResourceBuilder(CreateBrowserResource(serviceName));
            logging.AddOtlpExporter(options =>
                options.Endpoint = new Uri(otlpEndpoint, "v1/logs"));
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
                resource.AddService(serviceName, serviceInstanceId: serviceName))
            .WithMetrics(metrics =>
            {
                metrics.AddMeter("Microsoft.AspNetCore.Components");
                metrics.AddMeter("Microsoft.AspNetCore.Components.Lifecycle");
                metrics.AddHttpClientInstrumentation();
                metrics.AddOtlpExporter(options =>
                    options.Endpoint = new Uri(otlpEndpoint, "v1/metrics"));
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource("Microsoft.AspNetCore.Components");
                tracing.AddHttpClientInstrumentation();
                tracing.AddOtlpExporter(options =>
                    options.Endpoint = new Uri(otlpEndpoint, "v1/traces"));
            });
    }

    private static ResourceBuilder CreateBrowserResource(string serviceName)
    {
        return ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceInstanceId: serviceName);
    }
}
