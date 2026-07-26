using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace SymphonyTest1.Web.Infrastructure.Authentication;

internal sealed class TestingAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState AuthenticatedState = new(
        new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "test-administrator"),
                    new Claim(ClaimTypes.Name, "Test Administrator")
                ],
                authenticationType: "Testing")));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(AuthenticatedState);
}
