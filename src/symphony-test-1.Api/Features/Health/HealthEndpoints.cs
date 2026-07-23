using Dapper;
using SymphonyTest1.Api.Infrastructure;

namespace SymphonyTest1.Api.Features.Health;

public record HealthResponse(string Status, string Database, DateTime Timestamp);

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetHealth)
            .WithName("GetHealth")
            .WithTags("Health")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<IResult> GetHealth(
        IDbConnectionFactory connectionFactory,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("HealthEndpoints");
        logger.LogInformation("Health check requested");
        
        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync();
            var result = await connection.ExecuteScalarAsync<int>("SELECT 1");
            
            if (result == 1)
            {
                var response = new HealthResponse("Healthy", "Connected", DateTime.UtcNow);
                logger.LogInformation("Health check passed");
                return Results.Ok(response);
            }
            
            var unhealthyResponse = new HealthResponse("Unhealthy", "Query failed", DateTime.UtcNow);
            logger.LogWarning("Health check failed: database query did not return expected result");
            return Results.Json(unhealthyResponse, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health check failed: database connection error");
            var errorResponse = new HealthResponse("Unhealthy", $"Error: {ex.Message}", DateTime.UtcNow);
            return Results.Json(errorResponse, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
