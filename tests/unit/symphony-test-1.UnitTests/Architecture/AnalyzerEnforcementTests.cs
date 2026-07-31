using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SymphonyTest1.Analyzers;

namespace SymphonyTest1.UnitTests.Architecture;

[TestFixture]
public sealed class AnalyzerEnforcementTests
{
    private const string TestFeaturePath =
        "src/symphony-test-1.Api/Features/Greetings/BadSlice.cs";

    [Test]
    public async Task Analyzer_RejectsEveryRepositorySpecificViolation()
    {
        const string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Dapper
            {
                public readonly struct CommandDefinition
                {
                    public CommandDefinition(
                        string sql,
                        object? parameters = null,
                        CancellationToken cancellationToken = default) { }
                }
            }

            namespace Npgsql
            {
                public sealed class NpgsqlDataSource
                {
                    public Task OpenConnectionAsync(CancellationToken cancellationToken = default) =>
                        Task.CompletedTask;
                }
            }

            namespace FluentValidation
            {
                public interface IValidator<T>
                {
                    Task ValidateAsync(T value, CancellationToken cancellationToken);
                }
            }

            namespace Microsoft.AspNetCore.Http
            {
                public static class Results
                {
                    public static object Ok() => new();
                }
            }

            namespace SymphonyTest1.Api.Features.Greetings
            {
                public static class BadSlice
                {
                    public sealed record Request(Guid LanguageId);

                    private static async Task<object> Handle(
                        Request request,
                        FluentValidation.IValidator<Request> validator,
                        Npgsql.NpgsqlDataSource dataSource,
                        CancellationToken cancellationToken)
                    {
                        await dataSource.OpenConnectionAsync();
                        await validator.ValidateAsync(request, cancellationToken);
                        var command = new Dapper.CommandDefinition($"SELECT {request.LanguageId}");
                        return Microsoft.AspNetCore.Http.Results.Ok();
                    }
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source, TestFeaturePath);
        var diagnosticIds = diagnostics.Select(diagnostic => diagnostic.Id).ToHashSet();

        Assert.That(diagnosticIds, Is.SupersetOf(new[]
        {
            VerticalSliceAnalyzer.TypedIdDiagnosticId,
            VerticalSliceAnalyzer.CommandCancellationDiagnosticId,
            VerticalSliceAnalyzer.ConnectionCancellationDiagnosticId,
            VerticalSliceAnalyzer.TypedResultsDiagnosticId,
            VerticalSliceAnalyzer.ConstantSqlDiagnosticId,
            VerticalSliceAnalyzer.ValidationOrderDiagnosticId,
            VerticalSliceAnalyzer.SliceShapeDiagnosticId
        }));
    }

    [Test]
    public async Task Analyzer_AcceptsACompliantSlice()
    {
        const string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Dapper
            {
                public readonly struct CommandDefinition
                {
                    public CommandDefinition(
                        string sql,
                        object? parameters = null,
                        CancellationToken cancellationToken = default) { }
                }
            }

            namespace Npgsql
            {
                public sealed class NpgsqlDataSource
                {
                    public Task OpenConnectionAsync(CancellationToken cancellationToken) =>
                        Task.CompletedTask;
                }
            }

            namespace FluentValidation
            {
                public interface IValidator<T>
                {
                    Task ValidateAsync(T value, CancellationToken cancellationToken);
                }
            }

            namespace Microsoft.AspNetCore.Http.HttpResults
            {
                public sealed class Ok<T> { }
            }

            namespace SymphonyTest1.Api.Infrastructure.Identifiers
            {
                public readonly record struct LanguageId(Guid Value);
            }

            namespace SymphonyTest1.Api.Features.Greetings
            {
                public static class BadSlice
                {
                    public sealed record Request(
                        SymphonyTest1.Api.Infrastructure.Identifiers.LanguageId LanguageId);

                    public static void Map() { }

                    private static async Task<Microsoft.AspNetCore.Http.HttpResults.Ok<object>> Handle(
                        Request request,
                        FluentValidation.IValidator<Request> validator,
                        Npgsql.NpgsqlDataSource dataSource,
                        CancellationToken cancellationToken)
                    {
                        await validator.ValidateAsync(request, cancellationToken);
                        await dataSource.OpenConnectionAsync(cancellationToken);
                        const string sql = "SELECT 1";
                        var command = new Dapper.CommandDefinition(
                            sql,
                            cancellationToken: cancellationToken);
                        return new Microsoft.AspNetCore.Http.HttpResults.Ok<object>();
                    }
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source, TestFeaturePath);

        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task Analyzer_RejectsParsingAnEntityIdentifierAsARawGuid()
    {
        const string source = """
            using System;

            namespace SymphonyTest1.Api.Features.Greetings
            {
                public static class BadSlice
                {
                    public static void Map() { }

                    private static bool ParseObject(string value) =>
                        Guid.TryParse(value, out _);
                }
            }
            """;

        var diagnostics = await AnalyzeAsync(source, TestFeaturePath);

        Assert.That(
            diagnostics.Select(diagnostic => diagnostic.Id),
            Does.Contain(VerticalSliceAnalyzer.TypedIdDiagnosticId));
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string source,
        string path)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, path: path);
        var trustedPlatformAssemblies =
            (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException(
                "The runtime did not expose trusted platform assemblies.");
        var references = trustedPlatformAssemblies
            .Split(Path.PathSeparator)
            .Select(pathValue => MetadataReference.CreateFromFile(pathValue));
        var compilation = CSharpCompilation.Create(
            "AnalyzerTest",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new VerticalSliceAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }
}
