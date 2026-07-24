using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.OpenApi;
using SymphonyTest1.Api.Features.Greetings;
using SymphonyTest1.Api.Features.Health;
using SymphonyTest1.Api.Features.Languages;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi(options =>
{
    options.CreateSchemaReferenceId = jsonTypeInfo =>
    {
        var defaultId = OpenApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);
        var declaringType = jsonTypeInfo.Type.DeclaringType;

        return defaultId is null || declaringType is null
            ? defaultId
            : $"{declaringType.Name}{defaultId}";
    };
});
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.TraceId.ToString()
            ?? context.HttpContext.TraceIdentifier;
    };
});

builder.AddNpgsqlDataSource("DefaultConnection");
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.MapDefaultEndpoints();

app.MapGroup("/api/health")
    .MapHealthEndpoints();

app.MapGroup("/api/languages")
    .MapLanguageEndpoints();

app.MapGroup("/api/greetings")
    .MapGreetingEndpoints();

app.Run();

public partial class Program { }

