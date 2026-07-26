using System.ComponentModel;
using Dapper;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using SymphonyTest1.Api.Infrastructure.Time;

namespace SymphonyTest1.Api.Features.Greetings;

public static class UpdateGreeting
{
    /// <summary>Values required to update a greeting.</summary>
    /// <param name="LanguageId">The identifier of the language associated with the greeting.</param>
    /// <param name="GreetingText">The greeting text. Maximum length is 255 characters.</param>
    /// <param name="Formal">Whether the greeting is intended for formal contexts.</param>
    public sealed record Request(Guid LanguageId, string GreetingText, bool Formal);

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
        Guid Id,
        Guid LanguageId,
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
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<Response>, ValidationProblem, NotFound>> Handle(
        [Description("The unique greeting identifier.")] Guid id,
        Request request,
        IValidator<Request> validator,
        NpgsqlDataSource dataSource,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(UpdateGreeting).FullName!);
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
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

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
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

            logger.LogInformation("Updated greeting {GreetingId}", id);
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
        Guid Id,
        Guid LanguageId,
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
