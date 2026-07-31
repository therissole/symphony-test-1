using System.Security.Claims;
using Dapper;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using SymphonyTest1.Api.Infrastructure.Authorization;
using SymphonyTest1.Api.Infrastructure.Identifiers;
using SymphonyTest1.Api.Infrastructure.Time;

namespace SymphonyTest1.Api.Features.Greetings;

public static class ListGreetings
{
    /// <summary>Optional criteria used to narrow the greeting collection.</summary>
    /// <param name="LanguageId">Returns greetings associated with this language.</param>
    /// <param name="Formal">Returns greetings with this formality.</param>
    /// <param name="CreatedFrom">Inclusive RFC 3339 lower bound for the creation instant.</param>
    /// <param name="CreatedTo">Exclusive RFC 3339 upper bound for the creation instant.</param>
    public sealed record Request(
        LanguageId? LanguageId,
        bool? Formal,
        DateTimeOffset? CreatedFrom,
        DateTimeOffset? CreatedTo);

    internal sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(request => request.CreatedTo)
                .GreaterThan(request => request.CreatedFrom!.Value)
                .When(request => request.CreatedFrom.HasValue && request.CreatedTo.HasValue)
                .WithMessage("CreatedTo must be later than CreatedFrom.")
                .OverridePropertyName("createdTo");
        }
    }

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
        group.MapGet("/", Handle)
            .WithName("GetAllGreetings")
            .WithSummary("List greetings")
            .WithDescription("Returns every stored greeting, ordered by greeting text.")
            .Produces<List<Response>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();
    }

    private static async Task<Results<Ok<List<Response>>, ValidationProblem, ProblemHttpResult>> Handle(
        [AsParameters] Request request,
        IValidator<Request> validator,
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
                detail: "You do not have permission to view greetings.");
        }

        var objectIds = await authorization.ListObjectsAsync(user, "can_view", "greeting", cancellationToken);

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        var greetingIds = objectIds
            .Select(ParseGreetingObject)
            .OfType<GreetingId>()
            .ToArray();
        if (greetingIds.Length == 0)
        {
            return TypedResults.Ok<List<Response>>([]);
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
            WHERE
                id = ANY(@GreetingIds)
                AND
                (@HasLanguageId = FALSE OR language_id = @LanguageId)
                AND (@Formal IS NULL OR formal = @Formal)
                AND (CAST(@CreatedFrom AS TIMESTAMPTZ) IS NULL OR created_at >= CAST(@CreatedFrom AS TIMESTAMPTZ))
                AND (CAST(@CreatedTo AS TIMESTAMPTZ) IS NULL OR created_at < CAST(@CreatedTo AS TIMESTAMPTZ))
            ORDER BY greeting_text
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                GreetingIds = greetingIds.Select(greetingId => greetingId.Value).ToArray(),
                HasLanguageId = request.LanguageId.HasValue,
                LanguageId = request.LanguageId.GetValueOrDefault(),
                request.Formal,
                request.CreatedFrom,
                request.CreatedTo
            },
            cancellationToken: cancellationToken);
        var greetings = (await connection.QueryAsync<DatabaseResponse>(command))
            .Select(ToResponse)
            .ToList();

        return TypedResults.Ok(greetings);
    }

    private sealed record DatabaseResponse(
        GreetingId Id,
        LanguageId LanguageId,
        string GreetingText,
        bool Formal,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    private static GreetingId? ParseGreetingObject(string value)
    {
        const string prefix = "greeting:";
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && GreetingId.TryParse(value[prefix.Length..], provider: null, out var greetingId)
                ? greetingId
                : null;
    }

    private static Response ToResponse(DatabaseResponse value) =>
        new(
            value.Id,
            value.LanguageId,
            value.GreetingText,
            value.Formal,
            UtcInstant.FromDatabase(value.CreatedAt),
            UtcInstant.FromDatabase(value.UpdatedAt));
}
