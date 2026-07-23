namespace SymphonyTest1.Api.Features.Languages;

public static class LanguageEndpoints
{
    public static RouteGroupBuilder MapLanguageEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllLanguages)
            .WithName("GetAllLanguages")
            .WithTags("Languages")
            .Produces<IEnumerable<LanguageResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetLanguageById)
            .WithName("GetLanguageById")
            .WithTags("Languages")
            .Produces<LanguageResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateLanguage)
            .WithName("CreateLanguage")
            .WithTags("Languages")
            .Produces<LanguageResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPut("/{id:guid}", UpdateLanguage)
            .WithName("UpdateLanguage")
            .WithTags("Languages")
            .Produces<LanguageResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteLanguage)
            .WithName("DeleteLanguage")
            .WithTags("Languages")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetAllLanguages(
        ILanguageRepository repository,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("LanguageEndpoints");
        logger.LogInformation("Retrieving all languages");
        var languages = await repository.GetAllAsync();
        var response = languages.Select(l => new LanguageResponse(l.Id, l.Name, l.Code, l.CreatedAt, l.UpdatedAt));
        return Results.Ok(response);
    }

    private static async Task<IResult> GetLanguageById(
        Guid id,
        ILanguageRepository repository,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("LanguageEndpoints");
        logger.LogInformation("Retrieving language with id {LanguageId}", id);
        var language = await repository.GetByIdAsync(id);
        
        if (language is null)
        {
            logger.LogWarning("Language with id {LanguageId} not found", id);
            return Results.NotFound();
        }

        var response = new LanguageResponse(language.Id, language.Name, language.Code, language.CreatedAt, language.UpdatedAt);
        return Results.Ok(response);
    }

    private static async Task<IResult> CreateLanguage(
        CreateLanguageRequest request,
        ILanguageRepository repository,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("LanguageEndpoints");
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
        {
            logger.LogWarning("Invalid language creation request: Name or Code is empty");
            return Results.BadRequest("Name and Code are required");
        }

        logger.LogInformation("Creating language with code {LanguageCode}", request.Code);
        
        try
        {
            var id = await repository.CreateAsync(request);
            var language = await repository.GetByIdAsync(id);
            
            if (language is null)
            {
                return Results.Problem("Failed to retrieve created language");
            }

            var response = new LanguageResponse(language.Id, language.Name, language.Code, language.CreatedAt, language.UpdatedAt);
            return Results.Created($"/api/languages/{id}", response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating language with code {LanguageCode}", request.Code);
            return Results.BadRequest("Failed to create language. It may already exist.");
        }
    }

    private static async Task<IResult> UpdateLanguage(
        Guid id,
        UpdateLanguageRequest request,
        ILanguageRepository repository,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("LanguageEndpoints");
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
        {
            logger.LogWarning("Invalid language update request for id {LanguageId}: Name or Code is empty", id);
            return Results.BadRequest("Name and Code are required");
        }

        logger.LogInformation("Updating language with id {LanguageId}", id);
        
        try
        {
            var updated = await repository.UpdateAsync(id, request);
            
            if (!updated)
            {
                logger.LogWarning("Language with id {LanguageId} not found for update", id);
                return Results.NotFound();
            }

            var language = await repository.GetByIdAsync(id);
            
            if (language is null)
            {
                return Results.Problem("Failed to retrieve updated language");
            }

            var response = new LanguageResponse(language.Id, language.Name, language.Code, language.CreatedAt, language.UpdatedAt);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating language with id {LanguageId}", id);
            return Results.BadRequest("Failed to update language");
        }
    }

    private static async Task<IResult> DeleteLanguage(
        Guid id,
        ILanguageRepository repository,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("LanguageEndpoints");
        logger.LogInformation("Deleting language with id {LanguageId}", id);
        
        var deleted = await repository.DeleteAsync(id);
        
        if (!deleted)
        {
            logger.LogWarning("Language with id {LanguageId} not found for deletion", id);
            return Results.NotFound();
        }

        return Results.NoContent();
    }
}
