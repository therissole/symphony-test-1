using System.ComponentModel;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using SymphonyTest1.Api.Infrastructure.Time;

namespace SymphonyTest1.Api.Features.Languages;

public static class GetLanguage
{
    /// <summary>Represents a language in the catalog.</summary>
    /// <param name="Id">The unique language identifier.</param>
    /// <param name="Name">The human-readable language name.</param>
    /// <param name="Code">The short code used to identify the language.</param>
    /// <param name="CreatedAt">The UTC time when the language was created.</param>
    /// <param name="UpdatedAt">The UTC time when the language was last updated.</param>
    public sealed record Response(
        Guid Id,
        string Name,
        string Code,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", Handle)
            .WithName("GetLanguageById")
            .WithSummary("Get a language")
            .WithDescription("Returns a language from the catalog by its unique identifier.")
            .Produces<Response>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<Response>, NotFound>> Handle(
        [Description("The unique language identifier.")] Guid id,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id,
                name,
                code,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM languages
            WHERE id = @Id
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var databaseLanguage = await connection.QuerySingleOrDefaultAsync<DatabaseResponse>(command);

        return databaseLanguage is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(ToResponse(databaseLanguage));
    }

    private sealed record DatabaseResponse(Guid Id, string Name, string Code, DateTime CreatedAt, DateTime UpdatedAt);

    private static Response ToResponse(DatabaseResponse value) =>
        new(value.Id, value.Name, value.Code, UtcInstant.FromDatabase(value.CreatedAt), UtcInstant.FromDatabase(value.UpdatedAt));
}
