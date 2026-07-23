namespace SymphonyTest1.Api.Features.Greetings;

public static class GreetingsFeature
{
    public static RouteGroupBuilder MapGreetingEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Greetings");

        ListGreetings.Map(group);
        GetGreeting.Map(group);
        GetGreetingByLanguage.Map(group);
        CreateGreeting.Map(group);
        UpdateGreeting.Map(group);
        DeleteGreeting.Map(group);

        return group;
    }
}
