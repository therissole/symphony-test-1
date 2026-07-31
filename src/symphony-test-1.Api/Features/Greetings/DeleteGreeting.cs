using System.ComponentModel;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using SymphonyTest1.Api.Infrastructure.Authorization;
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
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Handle(
        [Description("The unique greeting identifier.")] GreetingId id,
        ClaimsPrincipal user,
        IOpenFgaAuthorization authorization,
        IOpenFgaTupleOutbox tupleOutbox,
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(DeleteGreeting).FullName!);
        var canManageCatalog = await authorization.IsAllowedAsync(
            user,
            relation: "can_manage_catalog",
            @object: "system:global",
            cancellationToken);
        if (!canManageCatalog)
        {
            LogGreetingDeletionForbidden(logger, id);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "You do not have permission to delete this greeting.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM greetings WHERE id = @Id)",
            new { Id = id },
            cancellationToken: cancellationToken));
        if (!exists)
        {
            return TypedResults.NotFound();
        }

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

        var tupleOperationId = await tupleOutbox.EnqueueAsync(
            OpenFgaTupleOperation.Delete,
            user: "system:global",
            relation: "system",
            @object: $"greeting:{id}",
            connection,
            transaction,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await tupleOutbox.DispatchAsync(tupleOperationId, cancellationToken);
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
