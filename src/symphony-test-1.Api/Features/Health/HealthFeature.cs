namespace SymphonyTest1.Api.Features.Health;

public static class HealthFeature
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Health");
        GetHealth.Map(group);

        return group;
    }
}
