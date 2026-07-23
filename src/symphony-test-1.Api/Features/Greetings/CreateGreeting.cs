using Dapper;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;

namespace SymphonyTest1.Api.Features.Greetings;

public static class CreateGreeting
{
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

    public sealed record Response(
        Guid Id,
        Guid LanguageId,
        string GreetingText,
        bool Formal,
        DateTime CreatedAt,
        DateTime UpdatedAt);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", Handle)
            .WithName("CreateGreeting")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesValidationProblem();
    }

    private static async Task<Results<Created<Response>, ValidationProblem>> Handle(
        Request request,
        IValidator<Request> validator,
        NpgsqlDataSource dataSource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(CreateGreeting).FullName!);
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        const string sql = """
            INSERT INTO greetings (language_id, greeting_text, formal)
            VALUES (@LanguageId, @GreetingText, @Formal)
            RETURNING
                id,
                language_id AS LanguageId,
                greeting_text AS GreetingText,
                formal,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, request, cancellationToken: cancellationToken);

        try
        {
            var greeting = await connection.QuerySingleAsync<Response>(command);
            logger.LogInformation(
                "Created greeting {GreetingId} for language {LanguageId}",
                greeting.Id,
                greeting.LanguageId);

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
}
