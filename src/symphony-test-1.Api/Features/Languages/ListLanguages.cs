using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using SymphonyTest1.Api.Infrastructure.Authorization;
using SymphonyTest1.Api.Infrastructure.Identifiers;
using SymphonyTest1.Api.Infrastructure.Time;

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
        LanguageId Id,
        string Name,
        string Code,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetAllLanguages")
            .WithSummary("List languages")
            .WithDescription("Returns every language in the catalog, ordered by name.")
            .Produces<List<Response>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<Results<Ok<List<Response>>, ProblemHttpResult>> Handle(
        ClaimsPrincipal user,
        IOpenFgaAuthorization authorization,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
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
                detail: "You do not have permission to view languages.");
        }

        var objectIds = await authorization.ListObjectsAsync(user, "can_view", "language", cancellationToken);
        var languageIds = objectIds
            .Select(ParseLanguageObject)
            .OfType<LanguageId>()
            .ToArray();
        if (languageIds.Length == 0)
        {
            return TypedResults.Ok<List<Response>>([]);
        }

        const string sql = """
            SELECT
                id,
                name,
                code,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM languages
            WHERE id = ANY(@LanguageIds)
            ORDER BY name
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { LanguageIds = languageIds.Select(languageId => languageId.Value).ToArray() },
            cancellationToken: cancellationToken);
        var languages = (await connection.QueryAsync<DatabaseResponse>(command))
            .Select(ToResponse)
            .ToList();

        return TypedResults.Ok(languages);
    }

    private sealed record DatabaseResponse(LanguageId Id, string Name, string Code, DateTime CreatedAt, DateTime UpdatedAt);

    private static LanguageId? ParseLanguageObject(string value)
    {
        const string prefix = "language:";
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && LanguageId.TryParse(value[prefix.Length..], provider: null, out var languageId)
                ? languageId
                : null;
    }

    private static Response ToResponse(DatabaseResponse value) =>
        new(value.Id, value.Name, value.Code, UtcInstant.FromDatabase(value.CreatedAt), UtcInstant.FromDatabase(value.UpdatedAt));
}
