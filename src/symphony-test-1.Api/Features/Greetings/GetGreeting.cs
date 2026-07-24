using System.ComponentModel;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace SymphonyTest1.Api.Features.Greetings;

public static class GetGreeting
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
        group.MapGet("/{id:guid}", Handle)
            .WithName("GetGreetingById")
            .WithSummary("Get a greeting")
            .WithDescription("Returns a stored greeting by its unique identifier.")
            .Produces<Response>()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<Response>, NotFound>> Handle(
        [Description("The unique greeting identifier.")] Guid id,
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
