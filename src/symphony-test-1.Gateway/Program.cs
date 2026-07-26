using Yarp.ReverseProxy.Configuration;

namespace SymphonyTest1.Gateway;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.AddServiceDefaults();
        builder.Services.AddReverseProxy();
        builder.Services.AddSingleton<IProxyConfigProvider>(services =>
            CreateProxyConfig(services.GetRequiredService<IConfiguration>()));

        var app = builder.Build();
        var webBaseUrl = app.Configuration["Gateway:WebBaseUrl"];

        if (app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.MapDefaultEndpoints();

        if (string.IsNullOrWhiteSpace(webBaseUrl))
        {
            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();
        }

        app.MapReverseProxy();

        if (string.IsNullOrWhiteSpace(webBaseUrl))
        {
            app.MapFallbackToFile(
                app.Environment.IsEnvironment("Testing")
                    ? "index.Testing.html"
                    : "index.html");
        }

        app.Run();
    }

    private static IProxyConfigProvider CreateProxyConfig(IConfiguration configuration)
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
