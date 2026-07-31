using System.Security.Claims;
using Dapper;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SymphonyTest1.Api.Infrastructure.Authorization;
using SymphonyTest1.Api.Infrastructure.Identifiers;
using SymphonyTest1.Api.Infrastructure.Time;

namespace SymphonyTest1.Api.Features.Languages;

public static partial class CreateLanguage
{
    /// <summary>Values required to create a language.</summary>
    /// <param name="Name">The human-readable language name. Maximum length is 100 characters.</param>
    /// <param name="Code">The unique short code for the language. Maximum length is 10 characters.</param>
    public sealed record Request(string Name, string Code);

    internal sealed class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(request => request.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("Name is required.")
                .MaximumLength(100)
                .WithMessage("Name must be 100 characters or fewer.")
                .OverridePropertyName("name");

            RuleFor(request => request.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .WithMessage("Code is required.")
                .MaximumLength(10)
                .WithMessage("Code must be 10 characters or fewer.")
                .OverridePropertyName("code");
        }
    }

    /// <summary>Represents the newly created language.</summary>
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
        group.MapPost("/", Handle)
            .WithName("CreateLanguage")
            .WithSummary("Create a language")
            .WithDescription("Adds a language with a unique name and code to the catalog.")
            .Produces<Response>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static async Task<Results<Created<Response>, ValidationProblem, Conflict<ProblemDetails>, ProblemHttpResult>> Handle(
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
        var logger = loggerFactory.CreateLogger(typeof(CreateLanguage).FullName!);
        var canCreateLanguage = await authorization.IsAllowedAsync(
            user,
            relation: "can_create_language",
            @object: "system:global",
            cancellationToken);
        if (!canCreateLanguage)
        {
            LogLanguageCreationForbidden(logger);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden",
                detail: "You do not have permission to create a language.");
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        const string sql = """
            INSERT INTO languages (name, code, created_at, updated_at)
            VALUES (@Name, @Code, @Now, @Now)
            RETURNING
                id,
                name,
                code,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { request.Name, request.Code, Now = timeProvider.GetUtcNow() },
            transaction: transaction,
            cancellationToken: cancellationToken);

        try
        {
            var databaseLanguage = await connection.QuerySingleAsync<DatabaseResponse>(command);
            var language = ToResponse(databaseLanguage);
            var tupleOperationId = await tupleOutbox.EnqueueAsync(
                OpenFgaTupleOperation.Write,
                user: "system:global",
                relation: "system",
                @object: $"language:{language.Id}",
                connection,
                transaction,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await tupleOutbox.DispatchAsync(tupleOperationId, cancellationToken);
            LogLanguageCreated(logger, language.Id, language.Code);

            return TypedResults.Created($"/api/languages/{language.Id}", language);
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Language already exists",
                Detail = "A language with the same name or code already exists.",
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    private sealed record DatabaseResponse(LanguageId Id, string Name, string Code, DateTime CreatedAt, DateTime UpdatedAt);

    private static Response ToResponse(DatabaseResponse value) =>
        new(value.Id, value.Name, value.Code, UtcInstant.FromDatabase(value.CreatedAt), UtcInstant.FromDatabase(value.UpdatedAt));

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Created language {LanguageId} with code {LanguageCode}")]
    private static partial void LogLanguageCreated(
        ILogger logger,
        LanguageId languageId,
        string languageCode);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Language creation was forbidden by OpenFGA")]
    private static partial void LogLanguageCreationForbidden(ILogger logger);
}
