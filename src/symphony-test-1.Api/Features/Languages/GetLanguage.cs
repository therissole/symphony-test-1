using System.ComponentModel;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using SymphonyTest1.Api.Infrastructure.Authorization;
using SymphonyTest1.Api.Infrastructure.Identifiers;
using SymphonyTest1.Api.Infrastructure.Time;

namespace SymphonyTest1.Api.Features.Languages;

public static class GetLanguage
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
        group.MapGet("/{id:guid}", Handle)
            .WithName("GetLanguageById")
            .WithSummary("Get a language")
            .WithDescription("Returns a language from the catalog by its unique identifier.")
            .Produces<Response>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<Response>, NotFound, ProblemHttpResult>> Handle(
        [Description("The unique language identifier.")] LanguageId id,
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
                detail: "You do not have permission to view this language.");
        }

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
        var databaseLanguage = await connection.QuerySingleOrDefaultAsync<DatabaseResponse>(command);
        if (databaseLanguage is null)
        {
            return TypedResults.NotFound();
        }

        var canViewLanguage = await authorization.IsAllowedAsync(
            user,
            relation: "can_view",
            @object: $"language:{id}",
            cancellationToken);
        if (!canViewLanguage)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "You do not have permission to view this language.");
        }
        return TypedResults.Ok(ToResponse(databaseLanguage));
    }

    private sealed record DatabaseResponse(LanguageId Id, string Name, string Code, DateTime CreatedAt, DateTime UpdatedAt);

    private static Response ToResponse(DatabaseResponse value) =>
        new(value.Id, value.Name, value.Code, UtcInstant.FromDatabase(value.CreatedAt), UtcInstant.FromDatabase(value.UpdatedAt));
}
