using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.OpenApi;
using SymphonyTest1.Api.Features.Authentication;
using SymphonyTest1.Api.Features.Greetings;
using SymphonyTest1.Api.Features.Health;
using SymphonyTest1.Api.Features.Languages;
using SymphonyTest1.Api.Infrastructure.Authentication;
using SymphonyTest1.Api.Infrastructure.Testing;

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
    options.AddBearerSecurity();
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
builder.Services.AddApplicationAuthentication(builder.Configuration);

var enableClockControl = builder.Configuration.GetValue<bool>("Testing:EnableClockControl");
if (enableClockControl && builder.Environment.IsProduction())
{
    throw new InvalidOperationException(
        "Test-environment clock control cannot be enabled in Production.");
}

if (enableClockControl)
{
    builder.Services.AddSingleton(new ControlledTimeProvider(TimeProvider.System));
    builder.Services.AddSingleton<TimeProvider>(serviceProvider =>
        serviceProvider.GetRequiredService<ControlledTimeProvider>());
}
else
{
    builder.Services.AddSingleton(TimeProvider.System);
}

builder.AddNpgsqlDataSource("DefaultConnection");
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

var api = app.MapGroup("/api")
    .RequireAuthorization();

api.MapGroup("/health")
    .AllowAnonymous()
    .MapHealthEndpoints();

api.MapGroup("/authentication")
    .AllowAnonymous()
    .MapAuthenticationEndpoints();

api.MapGroup("/languages")
    .MapLanguageEndpoints();

api.MapGroup("/greetings")
    .MapGreetingEndpoints();

if (enableClockControl)
{
    api.MapGroup("/testing")
        .MapTestClockEndpoints();
}

app.Run();

public partial class Program { }

