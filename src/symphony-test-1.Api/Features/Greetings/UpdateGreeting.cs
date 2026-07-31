using System.ComponentModel;
using System.Security.Claims;
using Dapper;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using SymphonyTest1.Api.Infrastructure.Authorization;
using SymphonyTest1.Api.Infrastructure.Identifiers;
using SymphonyTest1.Api.Infrastructure.Time;

namespace SymphonyTest1.Api.Features.Greetings;

public static partial class UpdateGreeting
{
    /// <summary>Values required to update a greeting.</summary>
    /// <param name="LanguageId">The identifier of the language associated with the greeting.</param>
    /// <param name="GreetingText">The greeting text. Maximum length is 255 characters.</param>
    /// <param name="Formal">Whether the greeting is intended for formal contexts.</param>
    public sealed record Request(LanguageId LanguageId, string GreetingText, bool Formal);

    internal sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(request => request.LanguageId)
                .NotEmpty()
                .WithMessage("LanguageId is required.")
                .OverridePropertyName("languageId");

            RuleFor(request => request.GreetingText)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("GreetingText is required.")
                .MaximumLength(255)
                .WithMessage("GreetingText must be 255 characters or fewer.")
                .OverridePropertyName("greetingText");
        }
    }

    /// <summary>Represents the updated greeting.</summary>
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
        group.MapPut("/{id:guid}", Handle)
            .WithName("UpdateGreeting")
            .WithSummary("Update a greeting")
            .WithDescription(
                "Replaces the language, text, and formality of an existing greeting.")
            .Produces<Response>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<Response>, ValidationProblem, NotFound, ProblemHttpResult>> Handle(
        [Description("The unique greeting identifier.")] GreetingId id,
        Request request,
        IValidator<Request> validator,
        ClaimsPrincipal user,
        IOpenFgaAuthorization authorization,
        NpgsqlDataSource dataSource,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(UpdateGreeting).FullName!);
        var canManageCatalog = await authorization.IsAllowedAsync(
            user,
            relation: "can_manage_catalog",
            @object: "system:global",
            cancellationToken);
        if (!canManageCatalog)
        {
            LogGreetingUpdateForbidden(logger, id);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "You do not have permission to update this greeting.");
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM greetings WHERE id = @Id)",
            new { Id = id },
            cancellationToken: cancellationToken));
        if (!exists)
        {
            return TypedResults.NotFound();
        }

        var canUpdateGreeting = await authorization.IsAllowedAsync(
            user,
            relation: "can_update",
            @object: $"greeting:{id}",
            cancellationToken);
        if (!canUpdateGreeting)
        {
            LogGreetingUpdateForbidden(logger, id);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "You do not have permission to update this greeting.");
        }

        const string sql = """
            UPDATE greetings
            SET
                language_id = @LanguageId,
                greeting_text = @GreetingText,
                formal = @Formal,
                updated_at = @Now
            WHERE id = @Id
            RETURNING
                id,
                language_id AS LanguageId,
                greeting_text AS GreetingText,
                formal,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            """;

        var command = new CommandDefinition(
            sql,
            new
            {
                Id = id,
                request.LanguageId,
                request.GreetingText,
                request.Formal,
                Now = timeProvider.GetUtcNow()
            },
            cancellationToken: cancellationToken);

        try
        {
            var databaseGreeting = await connection.QuerySingleOrDefaultAsync<DatabaseResponse>(command);
            if (databaseGreeting is null)
            {
                return TypedResults.NotFound();
            }

            var greeting = ToResponse(databaseGreeting);

            LogGreetingUpdated(logger, id);
            return TypedResults.Ok(greeting);
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["languageId"] = ["The specified language does not exist."]
            });
        }
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

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Updated greeting {GreetingId}")]
    private static partial void LogGreetingUpdated(ILogger logger, GreetingId greetingId);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Information,
        Message = "Greeting update was forbidden by OpenFGA for greeting {GreetingId}")]
    private static partial void LogGreetingUpdateForbidden(ILogger logger, GreetingId greetingId);
}
