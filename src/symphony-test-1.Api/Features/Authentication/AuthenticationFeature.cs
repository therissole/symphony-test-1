namespace SymphonyTest1.Api.Features.Authentication;

public static class AuthenticationFeature
{
    public static RouteGroupBuilder MapAuthenticationEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Authentication");
        GetAuthenticationConfiguration.Map(group);

        return group;
    }
}
