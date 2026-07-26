using System.ComponentModel;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace SymphonyTest1.Api.Features.Greetings;

public static class GetGreetingByLanguage
{
    /// <summary>Represents a greeting together with its language details.</summary>
    /// <param name="Language">The human-readable language name.</param>
    /// <param name="LanguageCode">The short code used to identify the language.</param>
    /// <param name="GreetingText">The greeting text returned to clients.</param>
    /// <param name="Formal">Whether the greeting is intended for formal contexts.</param>
    public sealed record Response(
        string Language,
        string LanguageCode,
        string GreetingText,
        bool Formal);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/by-language/{languageCode}", Handle)
            .WithName("GetGreetingByLanguage")
            .WithSummary("Get a greeting by language")
            .WithDescription(
                "Returns one greeting matching a language code and optional formality preference.")
            .Produces<Response>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<Response>, NotFound>> Handle(
        [Description("The short code of the language to match.")] string languageCode,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken,
        [Description("Whether to require a formal or informal greeting. Omit to match either.")]
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
