using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MudBlazor;
using MudBlazor.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using SymphonyTest1.ClientServiceDefaults;
using SymphonyTest1.Web;
using SymphonyTest1.Web.Infrastructure;
using SymphonyTest1.Web.Infrastructure.Authentication;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Configuration.AddEnvironmentVariables();
builder.AddBlazorClientServiceDefaults();
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

if (builder.HostEnvironment.IsEnvironment("Testing"))
{
    builder.Services.AddAuthorizationCore();
    builder.Services.AddScoped<AuthenticationStateProvider, TestingAuthenticationStateProvider>();
    builder.Services.AddScoped(_ => new HttpClient
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    });
}
else
{
    var configuredApiEndpoint = builder.Configuration["services:api:https:0"]
        ?? builder.Configuration["services:api:http:0"];
    var configuredApiBaseAddress = configuredApiEndpoint is null
        ? new Uri(new Uri(builder.HostEnvironment.BaseAddress), "../")
        : new Uri($"{configuredApiEndpoint.TrimEnd('/')}/", UriKind.Absolute);
    var apiClientBaseAddress = configuredApiEndpoint is null
        ? configuredApiBaseAddress
        : ApiAuthorizationMessageHandler.ServiceAddress;

    using var bootstrapClient = new HttpClient
    {
        BaseAddress = configuredApiBaseAddress
    };
    var authenticationConfiguration = await bootstrapClient.GetFromJsonAsync<AuthenticationConfiguration>(
        "api/authentication/configuration")
        ?? throw new InvalidOperationException(
            "The API returned an empty authentication configuration.");

    builder.Services.AddOidcAuthentication(options =>
    {
        options.ProviderOptions.Authority = authenticationConfiguration.Authority;
        options.ProviderOptions.ClientId = authenticationConfiguration.ClientId;
        options.ProviderOptions.ResponseType = "code";
        options.ProviderOptions.DefaultScopes.Clear();
        options.ProviderOptions.DefaultScopes.Add("openid");
        options.ProviderOptions.DefaultScopes.Add("profile");
        options.ProviderOptions.DefaultScopes.Add("email");
    });

    builder.Services.AddTransient(serviceProvider =>
        new ApiAuthorizationMessageHandler(
            serviceProvider.GetRequiredService<IAccessTokenProvider>(),
            serviceProvider.GetRequiredService<NavigationManager>())
            .ConfigureForApi(configuredApiBaseAddress, builder.HostEnvironment.BaseAddress));
    builder.Services.AddHttpClient(
            ApiAuthorizationMessageHandler.ClientName,
            client =>
            {
                client.BaseAddress = apiClientBaseAddress;
            })
        .AddHttpMessageHandler<ApiAuthorizationMessageHandler>();
    builder.Services.AddScoped(serviceProvider =>
        serviceProvider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient(ApiAuthorizationMessageHandler.ClientName));
}

builder.Services.AddMudServices(configuration =>
{
    configuration.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    configuration.SnackbarConfiguration.PreventDuplicates = true;
    configuration.SnackbarConfiguration.ShowCloseIcon = true;
});

var host = builder.Build();
_ = host.Services.GetService<MeterProvider>();
_ = host.Services.GetService<TracerProvider>();

await host.RunAsync();
