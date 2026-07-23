using FluentValidation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SymphonyTest1.Api.Features.Greetings;
using SymphonyTest1.Api.Features.Health;
using SymphonyTest1.Api.Features.Languages;
using SymphonyTest1.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("SymphonyTest1.Api"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource("Npgsql")
            .AddConsoleExporter();
    });

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();

app.MapGroup("/api/health")
    .MapHealthEndpoints();

app.MapGroup("/api/languages")
    .MapLanguageEndpoints();

app.MapGroup("/api/greetings")
    .MapGreetingEndpoints();

app.Run();

public partial class Program { }

