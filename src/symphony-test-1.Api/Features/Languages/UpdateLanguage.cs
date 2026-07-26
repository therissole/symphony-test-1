using System.ComponentModel;
using Dapper;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace SymphonyTest1.Api.Features.Languages;

public static class UpdateLanguage
{
    /// <summary>Values required to update a language.</summary>
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

    /// <summary>Represents the updated language.</summary>
    /// <param name="Id">The unique language identifier.</param>
    /// <param name="Name">The human-readable language name.</param>
    /// <param name="Code">The short code used to identify the language.</param>
    /// <param name="CreatedAt">The UTC time when the language was created.</param>
    /// <param name="UpdatedAt">The UTC time when the language was last updated.</param>
    public sealed record Response(
        Guid Id,
        string Name,
        string Code,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", Handle)
            .WithName("UpdateLanguage")
            .WithSummary("Update a language")
            .WithDescription("Replaces the name and code of an existing language.")
            .Produces<Response>()
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<Response>, ValidationProblem, Conflict<ProblemDetails>, NotFound>> Handle(
        [Description("The unique language identifier.")] Guid id,
        Request request,
        IValidator<Request> validator,
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(UpdateLanguage).FullName!);
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        const string sql = """
            UPDATE languages
            SET
                name = @Name,
                code = @Code,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @Id
            RETURNING
                id,
                name,
                code,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { Id = id, request.Name, request.Code },
            cancellationToken: cancellationToken);

        try
        {
            var language = await connection.QuerySingleOrDefaultAsync<Response>(command);
            if (language is null)
            {
                return TypedResults.NotFound();
            }

            logger.LogInformation("Updated language {LanguageId}", id);
            return TypedResults.Ok(language);
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
}
