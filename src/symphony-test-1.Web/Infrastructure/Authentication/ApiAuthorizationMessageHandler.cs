using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

namespace SymphonyTest1.Web.Infrastructure.Authentication;

internal sealed class ApiAuthorizationMessageHandler(
    IAccessTokenProvider provider,
    NavigationManager navigationManager)
    : AuthorizationMessageHandler(provider, navigationManager)
{
    public const string ClientName = "api";

    public static readonly Uri ServiceAddress = new("https+http://api");

    public ApiAuthorizationMessageHandler ConfigureForApi(Uri apiBaseAddress, string browserBaseAddress)
    {
        ConfigureHandler([ServiceAddress.AbsoluteUri, apiBaseAddress.AbsoluteUri, browserBaseAddress]);
        return this;
    }
}
