using Dapper;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace SymphonyTest1.Api.Features.Languages;

public static class CreateLanguage
{
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

    public sealed record Response(
        Guid Id,
        string Name,
        string Code,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", Handle)
            .WithName("CreateLanguage")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
    }

    private static async Task<Results<Created<Response>, ValidationProblem, Conflict<ProblemDetails>>> Handle(
        Request request,
        IValidator<Request> validator,
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(CreateLanguage).FullName!);
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        const string sql = """
            INSERT INTO languages (name, code)
            VALUES (@Name, @Code)
            RETURNING
                id,
                name,
                code,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, request, cancellationToken: cancellationToken);

        try
        {
            var language = await connection.QuerySingleAsync<Response>(command);
            logger.LogInformation(
                "Created language {LanguageId} with code {LanguageCode}",
                language.Id,
                language.Code);

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
}
