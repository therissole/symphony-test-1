using System.Security.Claims;
using Dapper;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using SymphonyTest1.Api.Infrastructure.Authorization;
using SymphonyTest1.Api.Infrastructure.Identifiers;
using SymphonyTest1.Api.Infrastructure.Time;

namespace SymphonyTest1.Api.Features.Greetings;

public static partial class CreateGreeting
{
    /// <summary>Values required to create a greeting.</summary>
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

    /// <summary>Represents the newly created greeting.</summary>
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
        group.MapPost("/", Handle)
            .WithName("CreateGreeting")
            .WithSummary("Create a greeting")
            .WithDescription("Adds a greeting for an existing language.")
            .Produces<Response>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem();
    }

    /// <summary>
    /// Authorizes greeting creation, validates the request, then persists and returns the new greeting.
    /// </summary>
    private static async Task<Results<Created<Response>, ValidationProblem, ProblemHttpResult>> Handle(
        Request request,
        IValidator<Request> validator,
        ClaimsPrincipal user,
        IOpenFgaAuthorization authorization,
        IOpenFgaTupleOutbox tupleOutbox,
        NpgsqlDataSource dataSource,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(CreateGreeting).FullName!);
        var canCreateGreeting = await authorization.IsAllowedAsync(
            user,
            relation: "can_create_greeting",
            @object: "system:global",
            cancellationToken);
        if (!canCreateGreeting)
        {
            LogGreetingCreationForbidden(logger);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "You do not have permission to create a greeting.");
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        const string sql = """
            INSERT INTO greetings (language_id, greeting_text, formal, created_at, updated_at)
            VALUES (@LanguageId, @GreetingText, @Formal, @Now, @Now)
            RETURNING
                id,
                language_id AS LanguageId,
                greeting_text AS GreetingText,
                formal,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new
            {
                request.LanguageId,
                request.GreetingText,
                request.Formal,
                Now = timeProvider.GetUtcNow()
            },
            transaction: transaction,
            cancellationToken: cancellationToken);

        try
        {
            var databaseGreeting = await connection.QuerySingleAsync<DatabaseResponse>(command);
            var greeting = ToResponse(databaseGreeting);
            var tupleOperationId = await tupleOutbox.EnqueueAsync(
                OpenFgaTupleOperation.Write,
                user: "system:global",
                relation: "system",
                @object: $"greeting:{greeting.Id}",
                connection,
                transaction,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await tupleOutbox.DispatchAsync(tupleOperationId, cancellationToken);
            LogGreetingCreated(logger, greeting.Id, greeting.LanguageId);

            return TypedResults.Created($"/api/greetings/{greeting.Id}", greeting);
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
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Created greeting {GreetingId} for language {LanguageId}")]
    private static partial void LogGreetingCreated(
        ILogger logger,
        GreetingId greetingId,
        LanguageId languageId);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Greeting creation was forbidden by OpenFGA")]
    private static partial void LogGreetingCreationForbidden(ILogger logger);
}
