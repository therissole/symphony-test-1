using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace SymphonyTest1.Api.Features.Health;

public static class GetHealth
{
    public sealed record Response(string Status, string Database, DateTime Timestamp);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetHealth")
            .Produces<Response>()
            .Produces<Response>(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<Results<Ok<Response>, JsonHttpResult<Response>>> Handle(
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(GetHealth).FullName!);

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var command = new CommandDefinition("SELECT 1", cancellationToken: cancellationToken);
            var result = await connection.ExecuteScalarAsync<int>(command);

            if (result == 1)
            {
                return TypedResults.Ok(
                    new Response("Healthy", "Connected", DateTime.UtcNow));
            }

            logger.LogWarning("Health check query did not return the expected result");
            return TypedResults.Json(
                new Response("Unhealthy", "Query failed", DateTime.UtcNow),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Health check could not reach the database");
            return TypedResults.Json(
                new Response("Unhealthy", "Unavailable", DateTime.UtcNow),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
