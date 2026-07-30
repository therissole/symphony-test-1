using System.ComponentModel;
using System.Security.Claims;

using Dapper;

using Microsoft.AspNetCore.Http.HttpResults;

using Npgsql;

using SymphonyTest1.Api.Infrastructure.Identifiers;
using SymphonyTest1.Api.Infrastructure.Authorization;

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
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Handle(
        [Description("The unique greeting identifier.")] GreetingId id,
        ClaimsPrincipal user,
        IOpenFgaAuthorization authorization,
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(DeleteGreeting).FullName!);
        var canDeleteGreeting = await authorization.IsAllowedAsync(
            user,
            relation: "can_delete",
            @object: $"greeting:{id}",
            cancellationToken);
        if (!canDeleteGreeting)
        {
            LogGreetingDeletionForbidden(logger, id);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "You do not have permission to delete this greeting.");
        }

        const string sql = "DELETE FROM greetings WHERE id = @Id";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { Id = id },
            transaction: transaction,
            cancellationToken: cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(command);

        if (rowsAffected == 0)
        {
            return TypedResults.NotFound();
        }

        await authorization.DeleteTupleAsync(
            user: "system:global",
            relation: "system",
            @object: $"greeting:{id}",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogGreetingDeleted(logger, id);
        return TypedResults.NoContent();
    }

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "Deleted greeting {GreetingId}")]
    private static partial void LogGreetingDeleted(ILogger logger, GreetingId greetingId);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "Greeting deletion was forbidden by OpenFGA for greeting {GreetingId}")]
    private static partial void LogGreetingDeletionForbidden(ILogger logger, GreetingId greetingId);

}
