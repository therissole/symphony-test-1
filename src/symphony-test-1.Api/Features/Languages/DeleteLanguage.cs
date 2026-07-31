using System.ComponentModel;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using SymphonyTest1.Api.Infrastructure.Authorization;
using SymphonyTest1.Api.Infrastructure.Identifiers;

namespace SymphonyTest1.Api.Features.Languages;

public static partial class DeleteLanguage
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", Handle)
            .WithName("DeleteLanguage")
            .WithSummary("Delete a language")
            .WithDescription("Deletes a language from the catalog by its unique identifier.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Handle(
        [Description("The unique language identifier.")] LanguageId id,
        ClaimsPrincipal user,
        IOpenFgaAuthorization authorization,
        IOpenFgaTupleOutbox tupleOutbox,
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(DeleteLanguage).FullName!);
        var canManageCatalog = await authorization.IsAllowedAsync(
            user,
            relation: "can_manage_catalog",
            @object: "system:global",
            cancellationToken);
        if (!canManageCatalog)
        {
            LogLanguageDeletionForbidden(logger, id);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "You do not have permission to delete this language.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM languages WHERE id = @Id)",
            new { Id = id },
            cancellationToken: cancellationToken));
        if (!exists)
        {
            return TypedResults.NotFound();
        }

        var canDeleteLanguage = await authorization.IsAllowedAsync(
            user,
            relation: "can_delete",
            @object: $"language:{id}",
            cancellationToken);
        if (!canDeleteLanguage)
        {
            LogLanguageDeletionForbidden(logger, id);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "You do not have permission to delete this language.");
        }

        const string lockLanguageSql = "SELECT id FROM languages WHERE id = @Id FOR UPDATE";
        // Lock the children before deriving tuple deletes so a concurrent update cannot move a
        // greeting out of the cascade after its tuple has been selected for deletion.
        const string relatedGreetingsSql =
            "SELECT id FROM greetings WHERE language_id = @Id FOR UPDATE";
        const string sql = "DELETE FROM languages WHERE id = @Id";

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var lockedLanguageId = await connection.QuerySingleOrDefaultAsync<LanguageId>(
            new CommandDefinition(
                lockLanguageSql,
                new { Id = id },
                transaction: transaction,
                cancellationToken: cancellationToken));
        if (lockedLanguageId == default)
        {
            return TypedResults.NotFound();
        }

        var relatedGreetings = await connection.QueryAsync<GreetingId>(new CommandDefinition(
            relatedGreetingsSql,
            new { Id = id },
            transaction: transaction,
            cancellationToken: cancellationToken));
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

        var tupleOperationIds = new List<Guid>();
        foreach (var greetingId in relatedGreetings)
        {
            tupleOperationIds.Add(await tupleOutbox.EnqueueAsync(
                OpenFgaTupleOperation.Delete,
                user: "system:global",
                relation: "system",
                @object: $"greeting:{greetingId}",
                connection,
                transaction,
                cancellationToken));
        }

        tupleOperationIds.Add(await tupleOutbox.EnqueueAsync(
            OpenFgaTupleOperation.Delete,
            user: "system:global",
            relation: "system",
            @object: $"language:{id}",
            connection,
            transaction,
            cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        foreach (var tupleOperationId in tupleOperationIds)
        {
            await tupleOutbox.DispatchAsync(tupleOperationId, cancellationToken);
        }

        LogLanguageDeleted(logger, id);
        return TypedResults.NoContent();
    }

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Deleted language {LanguageId}")]
    private static partial void LogLanguageDeleted(ILogger logger, LanguageId languageId);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Information,
        Message = "Language deletion was forbidden by OpenFGA for language {LanguageId}")]
    private static partial void LogLanguageDeletionForbidden(ILogger logger, LanguageId languageId);
}
