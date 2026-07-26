using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace SymphonyTest1.Api.Features.Health;

public static class GetHealth
{
    /// <summary>Describes the API and database health at the time of the check.</summary>
    /// <param name="Status">The overall health state.</param>
    /// <param name="Database">The database connectivity state.</param>
    /// <param name="Timestamp">The UTC time when the health check completed.</param>
    public sealed record Response(string Status, string Database, DateTimeOffset Timestamp);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetHealth")
            .WithSummary("Get health")
            .WithDescription(
                "Checks whether the API can connect to PostgreSQL and execute a simple query.")
            .Produces<Response>()
            .Produces<Response>(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<Results<Ok<Response>, JsonHttpResult<Response>>> Handle(
        NpgsqlDataSource dataSource,
        TimeProvider timeProvider,
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
                    new Response("Healthy", "Connected", timeProvider.GetUtcNow()));
            }

            logger.LogWarning("Health check query did not return the expected result");
            return TypedResults.Json(
                new Response("Unhealthy", "Query failed", timeProvider.GetUtcNow()),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Health check could not reach the database");
            return TypedResults.Json(
                new Response("Unhealthy", "Unavailable", timeProvider.GetUtcNow()),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
