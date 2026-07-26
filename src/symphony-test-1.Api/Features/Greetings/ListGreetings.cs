using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace SymphonyTest1.Api.Features.Greetings;

public static class ListGreetings
{
    /// <summary>Represents a stored greeting.</summary>
    /// <param name="Id">The unique greeting identifier.</param>
    /// <param name="LanguageId">The identifier of the language associated with the greeting.</param>
    /// <param name="GreetingText">The greeting text returned to clients.</param>
    /// <param name="Formal">Whether the greeting is intended for formal contexts.</param>
    /// <param name="CreatedAt">The UTC time when the greeting was created.</param>
    /// <param name="UpdatedAt">The UTC time when the greeting was last updated.</param>
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
            .WithSummary("List greetings")
            .WithDescription("Returns every stored greeting, ordered by greeting text.")
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
