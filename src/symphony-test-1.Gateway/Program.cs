using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Configuration;

namespace SymphonyTest1.Gateway;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();
        builder.WebHost.UseStaticWebAssets();

        var clientApps = builder.Configuration
            .GetSection("ClientApps")
            .Get<Dictionary<string, ClientAppConfiguration>>() ?? [];

        if (clientApps.Count > 0)
        {
            builder.Services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
                .AddServiceDiscoveryDestinationResolver();
        }
        else
        {
            builder.Services.AddReverseProxy();
            builder.Services.AddSingleton<IProxyConfigProvider>(services =>
                CreateProxyConfig(services.GetRequiredService<IConfiguration>()));
        }

        var app = builder.Build();
        var webBaseUrl = app.Configuration["Gateway:WebBaseUrl"];

        if (app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.MapDefaultEndpoints();
        app.MapHealthChecks("/health");

        if (clientApps.Count > 0)
        {
            app.MapReverseProxy();

            string? defaultClientAppPath = null;
            foreach (var clientApp in clientApps.Values)
            {
                if (clientApp.ConfigEndpointPath is null
                    || clientApp.ConfigResponse is null
                    || clientApp.PathPrefix is null
                    || clientApp.EndpointsManifest is null)
                {
                    throw new InvalidOperationException(
                        "Aspire supplied an incomplete Blazor client application configuration.");
                }

                app.MapGet(clientApp.ConfigEndpointPath, () => Results.Content(
                    clientApp.ConfigResponse!,
                    "application/json"))
                    .WithMetadata(new ContentEncodingMetadata("identity", 1.0));

                app.MapGroup(clientApp.PathPrefix)
                    .MapStaticAssets(clientApp.EndpointsManifest)
                    .Add(endpoint =>
                    {
                        if (endpoint is RouteEndpointBuilder routeEndpoint
                            && routeEndpoint.RoutePattern.RawText?.Contains("{**path") == true)
                        {
                            routeEndpoint.Order = int.MaxValue;
                        }
                    });

                defaultClientAppPath ??= clientApp.PathPrefix;
            }

            if (defaultClientAppPath is not null)
            {
                app.MapGet("/", () => Results.Redirect(defaultClientAppPath));
            }
        }
        else if (string.IsNullOrWhiteSpace(webBaseUrl))
        {
            if (app.Environment.IsEnvironment("Testing"))
            {
                app.UseBlazorFrameworkFiles();
                app.UseStaticFiles();
                app.MapReverseProxy();
                app.MapFallbackToFile("index.Testing.html");
            }
            else
            {
                app.UseBlazorFrameworkFiles();
                app.UseStaticFiles();
                app.MapReverseProxy();
                app.MapFallbackToFile("index.Standalone.html");
            }
        }
        else
        {
            app.MapReverseProxy();
        }

        app.Run();
    }

    public sealed class ClientAppConfiguration
    {
        public string? PathPrefix { get; set; }
        public string? EndpointsManifest { get; set; }
        public string? ConfigEndpointPath { get; set; }
        public string? ConfigResponse { get; set; }
    }

    private static InMemoryConfigProvider CreateProxyConfig(IConfiguration configuration)
    {
        var apiBaseUrl = GetRequiredBaseUrl(configuration, "Gateway:ApiBaseUrl");
        var webBaseUrl = configuration["Gateway:WebBaseUrl"];
        var routes = new List<RouteConfig>
        {
            new()
            {
                RouteId = "api",
                ClusterId = "api",
                Order = 0,
                Match = new RouteMatch { Path = "/api/{**catch-all}" }
            }
        };
        var clusters = new List<ClusterConfig>
        {
            CreateCluster("api", apiBaseUrl)
        };

        if (!string.IsNullOrWhiteSpace(webBaseUrl))
        {
            routes.Add(new RouteConfig
            {
                RouteId = "web",
                ClusterId = "web",
                Order = 100,
                Match = new RouteMatch { Path = "/{**catch-all}" }
            });
            clusters.Add(CreateCluster("web", webBaseUrl));
        }

        return new InMemoryConfigProvider(routes, clusters);
    }

    private static ClusterConfig CreateCluster(string clusterId, string baseUrl)
    {
        var address = new Uri(baseUrl, UriKind.Absolute).AbsoluteUri;
        if (!address.EndsWith('/'))
        {
            address += "/";
        }

        return new ClusterConfig
        {
            ClusterId = clusterId,
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["primary"] = new() { Address = address }
            }
        };
    }

    private static string GetRequiredBaseUrl(IConfiguration configuration, string key)
    {
        return configuration[key]
            ?? throw new InvalidOperationException(
                $"Configuration value '{key}' is required.");
    }
}
