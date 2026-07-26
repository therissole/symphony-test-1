using System.ComponentModel;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace SymphonyTest1.Api.Features.Languages;

public static class DeleteLanguage
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", Handle)
            .WithName("DeleteLanguage")
            .WithSummary("Delete a language")
            .WithDescription("Deletes a language from the catalog by its unique identifier.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<NoContent, NotFound>> Handle(
        [Description("The unique language identifier.")] Guid id,
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(DeleteLanguage).FullName!);
        const string sql = "DELETE FROM languages WHERE id = @Id";

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(command);

        if (rowsAffected == 0)
        {
            return TypedResults.NotFound();
        }

        logger.LogInformation("Deleted language {LanguageId}", id);
        return TypedResults.NoContent();
    }
}
