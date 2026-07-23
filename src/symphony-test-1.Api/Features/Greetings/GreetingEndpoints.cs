using Dapper;
using SymphonyTest1.Api.Infrastructure;

namespace SymphonyTest1.Api.Features.Greetings;

public static class GreetingEndpoints
{
    public static RouteGroupBuilder MapGreetingEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllGreetings)
            .WithName("GetAllGreetings")
            .WithTags("Greetings")
            .Produces<IEnumerable<GreetingResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetGreetingById)
            .WithName("GetGreetingById")
            .WithTags("Greetings")
            .Produces<GreetingResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/by-language/{languageCode}", GetGreetingByLanguage)
            .WithName("GetGreetingByLanguage")
            .WithTags("Greetings")
            .Produces<GreetingByLanguageResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateGreeting)
            .WithName("CreateGreeting")
            .WithTags("Greetings")
            .Produces<GreetingResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", UpdateGreeting)
            .WithName("UpdateGreeting")
            .WithTags("Greetings")
            .Produces<GreetingResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteGreeting)
            .WithName("DeleteGreeting")
            .WithTags("Greetings")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetAllGreetings(
        IGreetingRepository repository,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("GreetingEndpoints");
        logger.LogInformation("Retrieving all greetings");
        var greetings = await repository.GetAllAsync();
        var response = greetings.Select(g => new GreetingResponse(g.Id, g.LanguageId, g.GreetingText, g.Formal, g.CreatedAt, g.UpdatedAt));
        return Results.Ok(response);
    }

    private static async Task<IResult> GetGreetingById(
        Guid id,
        IGreetingRepository repository,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("GreetingEndpoints");
        logger.LogInformation("Retrieving greeting with id {GreetingId}", id);
        var greeting = await repository.GetByIdAsync(id);
        
        if (greeting is null)
        {
            logger.LogWarning("Greeting with id {GreetingId} not found", id);
            return Results.NotFound();
        }

        var response = new GreetingResponse(greeting.Id, greeting.LanguageId, greeting.GreetingText, greeting.Formal, greeting.CreatedAt, greeting.UpdatedAt);
        return Results.Ok(response);
    }

    private static async Task<IResult> GetGreetingByLanguage(
        string languageCode,
        IGreetingRepository greetingRepository,
        IDbConnectionFactory connectionFactory,
        ILoggerFactory loggerFactory,
        bool? formal = null)
    {
        var logger = loggerFactory.CreateLogger("GreetingEndpoints");
        logger.LogInformation("Retrieving greeting for language code {LanguageCode}, formal: {Formal}", languageCode, formal);
        
        var greeting = await greetingRepository.GetByLanguageCodeAsync(languageCode, formal);
        
        if (greeting is null)
        {
            logger.LogWarning("No greeting found for language code {LanguageCode}", languageCode);
            return Results.NotFound($"No greeting found for language: {languageCode}");
        }

        using var connection = await connectionFactory.CreateConnectionAsync();
        const string sql = "SELECT name, code FROM languages WHERE id = @LanguageId";
        var language = await connection.QuerySingleOrDefaultAsync<(string name, string code)>(sql, new { greeting.LanguageId });

        var response = new GreetingByLanguageResponse(language.name, language.code, greeting.GreetingText, greeting.Formal);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateGreeting(
        CreateGreetingRequest request,
        IGreetingRepository repository,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("GreetingEndpoints");
        if (string.IsNullOrWhiteSpace(request.GreetingText))
        {
            logger.LogWarning("Invalid greeting creation request: GreetingText is empty");
            return Results.BadRequest("GreetingText is required");
        }

        logger.LogInformation("Creating greeting for language id {LanguageId}", request.LanguageId);
        
        try
        {
            var id = await repository.CreateAsync(request);
            var greeting = await repository.GetByIdAsync(id);
            
            if (greeting is null)
            {
                return Results.Problem("Failed to retrieve created greeting");
            }

            var response = new GreetingResponse(greeting.Id, greeting.LanguageId, greeting.GreetingText, greeting.Formal, greeting.CreatedAt, greeting.UpdatedAt);
            return Results.Created($"/api/greetings/{id}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating greeting for language id {LanguageId}", request.LanguageId);
            return Results.BadRequest("Failed to create greeting. The language may not exist.");
        }
    }

    private static async Task<IResult> UpdateGreeting(
        Guid id,
        UpdateGreetingRequest request,
        IGreetingRepository repository,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("GreetingEndpoints");
        if (string.IsNullOrWhiteSpace(request.GreetingText))
        {
            logger.LogWarning("Invalid greeting update request for id {GreetingId}: GreetingText is empty", id);
            return Results.BadRequest("GreetingText is required");
        }

        logger.LogInformation("Updating greeting with id {GreetingId}", id);
        
        try
        {
            var updated = await repository.UpdateAsync(id, request);
            
            if (!updated)
            {
                logger.LogWarning("Greeting with id {GreetingId} not found for update", id);
                return Results.NotFound();
            }

            var greeting = await repository.GetByIdAsync(id);
            
            if (greeting is null)
            {
                return Results.Problem("Failed to retrieve updated greeting");
            }

            var response = new GreetingResponse(greeting.Id, greeting.LanguageId, greeting.GreetingText, greeting.Formal, greeting.CreatedAt, greeting.UpdatedAt);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating greeting with id {GreetingId}", id);
            return Results.BadRequest("Failed to update greeting");
        }
    }

    private static async Task<IResult> DeleteGreeting(
        Guid id,
        IGreetingRepository repository,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("GreetingEndpoints");
        logger.LogInformation("Deleting greeting with id {GreetingId}", id);
        
        var deleted = await repository.DeleteAsync(id);
        
        if (!deleted)
        {
            logger.LogWarning("Greeting with id {GreetingId} not found for deletion", id);
            return Results.NotFound();
        }

        return Results.NoContent();
    }
}
