using Microsoft.AspNetCore.Http.HttpResults;

namespace SymphonyTest1.Api.Features.Authentication;

public static class GetAuthenticationConfiguration
{
    /// <summary>Provides the public settings required to start an OIDC browser flow.</summary>
    /// <param name="Authority">The Keycloak realm authority for the current environment.</param>
    /// <param name="ClientId">The public OIDC client identifier for the administration UI.</param>
    public sealed record Response(string Authority, string ClientId);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/configuration", Handle)
            .WithName("GetAuthenticationConfiguration")
            .WithSummary("Get authentication configuration")
            .WithDescription(
                "Returns non-secret OIDC settings for the administration UI in the current environment.")
            .Produces<Response>();
    }

    private static Ok<Response> Handle(IConfiguration configuration)
    {
        var authority = configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException(
                "Authentication:Authority must be configured.");
        var clientId = configuration["Authentication:WebClientId"]
            ?? throw new InvalidOperationException(
                "Authentication:WebClientId must be configured.");

        return TypedResults.Ok(new Response(authority, clientId));
    }
}
