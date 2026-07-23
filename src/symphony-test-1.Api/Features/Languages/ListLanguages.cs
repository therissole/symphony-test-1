using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace SymphonyTest1.Api.Features.Languages;

public static class ListLanguages
{
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
            .Produces<List<Response>>();
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
