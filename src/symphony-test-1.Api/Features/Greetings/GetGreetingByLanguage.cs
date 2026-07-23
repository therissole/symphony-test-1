using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace SymphonyTest1.Api.Features.Greetings;

public static class GetGreetingByLanguage
{
    public sealed record Response(
        string Language,
        string LanguageCode,
        string GreetingText,
        bool Formal);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/by-language/{languageCode}", Handle)
            .WithName("GetGreetingByLanguage")
            .Produces<Response>()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<Response>, NotFound>> Handle(
        string languageCode,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken,
        bool? formal = null)
    {
        const string sql = """
            SELECT
                l.name AS Language,
                l.code AS LanguageCode,
                g.greeting_text AS GreetingText,
                g.formal
            FROM greetings AS g
            INNER JOIN languages AS l ON l.id = g.language_id
            WHERE
                l.code = @LanguageCode
                AND (@Formal IS NULL OR g.formal = @Formal)
            ORDER BY g.formal, g.id
            LIMIT 1
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { LanguageCode = languageCode, Formal = formal },
            cancellationToken: cancellationToken);
        var greeting = await connection.QuerySingleOrDefaultAsync<Response>(command);

        return greeting is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(greeting);
    }
}
