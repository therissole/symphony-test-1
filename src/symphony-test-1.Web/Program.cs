using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using MudBlazor;
using MudBlazor.Services;
using SymphonyTest1.Web;
using SymphonyTest1.Web.Infrastructure.Authentication;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
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
    using var bootstrapClient = new HttpClient
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
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

    builder.Services.AddScoped(serviceProvider =>
    {
        var handler = serviceProvider
            .GetRequiredService<AuthorizationMessageHandler>()
            .ConfigureHandler([builder.HostEnvironment.BaseAddress]);
        handler.InnerHandler = new HttpClientHandler();

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
        };
    });
}

builder.Services.AddMudServices(configuration =>
{
    configuration.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    configuration.SnackbarConfiguration.PreventDuplicates = true;
    configuration.SnackbarConfiguration.ShowCloseIcon = true;
});

await builder.Build().RunAsync();
