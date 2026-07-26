using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace SymphonyTest1.Api.Features.Languages;

public static class ListLanguages
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
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetAllLanguages")
            .WithSummary("List languages")
            .WithDescription("Returns every language in the catalog, ordered by name.")
            .Produces<List<Response>>()
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<Ok<List<Response>>> Handle(
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
            ORDER BY name
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var languages = (await connection.QueryAsync<Response>(command)).AsList();

        return TypedResults.Ok(languages);
    }
}
