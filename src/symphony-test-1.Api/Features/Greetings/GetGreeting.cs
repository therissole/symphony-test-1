using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace SymphonyTest1.Api.Features.Greetings;

public static class GetGreeting
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
        group.MapGet("/{id:guid}", Handle)
            .WithName("GetGreetingById")
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
                language_id AS LanguageId,
                greeting_text AS GreetingText,
                formal,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM greetings
            WHERE id = @Id
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken);
        var greeting = await connection.QuerySingleOrDefaultAsync<Response>(command);

        return greeting is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(greeting);
    }
}
