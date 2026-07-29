using System.ComponentModel;

using Dapper;

using Microsoft.AspNetCore.Http.HttpResults;

using Npgsql;

using SymphonyTest1.Api.Infrastructure.Identifiers;

namespace SymphonyTest1.Api.Features.Greetings;

public static partial class DeleteGreeting
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", Handle)
            .WithName("DeleteGreeting")
            .WithSummary("Delete a greeting")
            .WithDescription("Deletes a greeting by its unique identifier.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<NoContent, NotFound>> Handle(
        [Description("The unique greeting identifier.")] GreetingId id,
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(DeleteGreeting).FullName!);
        const string sql = "DELETE FROM greetings WHERE id = @Id";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(command);

        if (rowsAffected == 0)
        {
            return TypedResults.NotFound();
        }

        LogGreetingDeleted(logger, id);
        return TypedResults.NoContent();
    }

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "Deleted greeting {GreetingId}")]
    private static partial void LogGreetingDeleted(ILogger logger, GreetingId greetingId);
}
