using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace SymphonyTest1.Api.Features.Greetings;

public static class ListGreetings
{
    public sealed record Response(
        Guid Id,
        Guid LanguageId,
        string GreetingText,
        bool Formal,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetAllGreetings")
            .Produces<List<Response>>();
    }

    private static async Task<Ok<List<Response>>> Handle(
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                id,
                language_id AS LanguageId,
                greeting_text AS GreetingText,
                formal,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM greetings
            ORDER BY greeting_text
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken);
        var greetings = (await connection.QueryAsync<Response>(command)).AsList();

        return TypedResults.Ok(greetings);
    }
}
