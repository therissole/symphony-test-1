using Microsoft.AspNetCore.Http.HttpResults;

namespace SymphonyTest1.Api.Infrastructure.Testing;

public static class TestClockEndpoints
{
    public sealed record SetRequest(DateTimeOffset UtcNow);
    public sealed record Response(DateTimeOffset UtcNow);

    public static RouteGroupBuilder MapTestClockEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Testing");

        group.MapGet("/clock", Get)
            .WithSummary("Get the test-environment clock")
            .Produces<Response>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPut("/clock", Set)
            .WithSummary("Set the test-environment clock")
            .Produces<Response>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapDelete("/clock", Reset)
            .WithSummary("Reset the test-environment clock")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static Ok<Response> Get(ControlledTimeProvider timeProvider) =>
        TypedResults.Ok(new Response(timeProvider.GetUtcNow()));

    private static Ok<Response> Set(SetRequest request, ControlledTimeProvider timeProvider)
    {
        timeProvider.SetUtcNow(request.UtcNow);
        return TypedResults.Ok(new Response(timeProvider.GetUtcNow()));
    }

    private static NoContent Reset(ControlledTimeProvider timeProvider)
    {
        timeProvider.Reset();
        return TypedResults.NoContent();
    }
}
