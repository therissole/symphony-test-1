using System.ComponentModel;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using SymphonyTest1.Api.Infrastructure.Authorization;
using SymphonyTest1.Api.Infrastructure.Identifiers;
using SymphonyTest1.Api.Infrastructure.Time;

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
        GreetingId Id,
        LanguageId LanguageId,
        string GreetingText,
        bool Formal,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", Handle)
            .WithName("GetGreetingById")
            .WithSummary("Get a greeting")
            .WithDescription("Returns a stored greeting by its unique identifier.")
            .Produces<Response>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<Response>, NotFound, ProblemHttpResult>> Handle(
        [Description("The unique greeting identifier.")] GreetingId id,
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
                detail: "You do not have permission to view this greeting.");
        }

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
        var databaseGreeting = await connection.QuerySingleOrDefaultAsync<DatabaseResponse>(command);
        if (databaseGreeting is null)
        {
            return TypedResults.NotFound();
        }

        var canViewGreeting = await authorization.IsAllowedAsync(
            user,
            relation: "can_view",
            @object: $"greeting:{id}",
            cancellationToken);
        if (!canViewGreeting)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "You do not have permission to view this greeting.");
        }
        return TypedResults.Ok(ToResponse(databaseGreeting));
    }

    private sealed record DatabaseResponse(
        GreetingId Id,
        LanguageId LanguageId,
        string GreetingText,
        bool Formal,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private static Response ToResponse(DatabaseResponse value) =>
        new(
            value.Id,
            value.LanguageId,
            value.GreetingText,
            value.Formal,
            UtcInstant.FromDatabase(value.CreatedAt),
            UtcInstant.FromDatabase(value.UpdatedAt));
}
