using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace SymphonyTest1.Api.Features.Languages;

public static class GetLanguage
{
    public sealed record Response(
        Guid Id,
        string Name,
        string Code,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", Handle)
            .WithName("GetLanguageById")
            .Produces<Response>()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<Response>, NotFound>> Handle(
        Guid id,
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
        var language = await connection.QuerySingleOrDefaultAsync<Response>(command);

        return language is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(language);
    }
}
