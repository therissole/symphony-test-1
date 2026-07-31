using System.ComponentModel;
using System.Security.Claims;

using Dapper;

using Microsoft.AspNetCore.Http.HttpResults;

using Npgsql;

using SymphonyTest1.Api.Infrastructure.Authorization;
using SymphonyTest1.Api.Infrastructure.Identifiers;

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
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<Response>, NotFound, ProblemHttpResult>> Handle(
        [Description("The short code of the language to match.")] string languageCode,
        ClaimsPrincipal user,
        IOpenFgaAuthorization authorization,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken,
        [Description("Whether to require a formal or informal greeting. Omit to match either.")]
        bool? formal = null)
    {
        var canReadCatalog = await authorization.IsAllowedAsync(
            user,
            relation: "can_read_catalog",
            @object: "system:global",
            cancellationToken);
        if (!canReadCatalog)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "You do not have permission to view greetings.");
        }

        var objectIds = await authorization.ListObjectsAsync(user, "can_view", "greeting", cancellationToken);
        var greetingIds = objectIds
            .Select(ParseGreetingObject)
            .OfType<GreetingId>()
            .ToArray();
        const string sql = """
            SELECT
                l.name AS Language,
                l.code AS LanguageCode,
                g.greeting_text AS GreetingText,
                g.formal
            FROM greetings AS g
            INNER JOIN languages AS l ON l.id = g.language_id
            WHERE
                g.id = ANY(@GreetingIds)
                AND
                l.code = @LanguageCode
                AND (@Formal IS NULL OR g.formal = @Formal)
            ORDER BY g.formal, g.id
            LIMIT 1
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                GreetingIds = greetingIds.Select(greetingId => greetingId.Value).ToArray(),
                LanguageCode = languageCode,
                Formal = formal
            },
            cancellationToken: cancellationToken);
        var greeting = await connection.QuerySingleOrDefaultAsync<Response>(command);

        return greeting is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(greeting);
    }

    private static GreetingId? ParseGreetingObject(string value)
    {
        const string prefix = "greeting:";
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && GreetingId.TryParse(value[prefix.Length..], provider: null, out var greetingId)
                ? greetingId
                : null;
    }
}
