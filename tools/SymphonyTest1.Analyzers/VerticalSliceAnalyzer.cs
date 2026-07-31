using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SymphonyTest1.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class VerticalSliceAnalyzer : DiagnosticAnalyzer
{
    public const string TypedIdDiagnosticId = "SYM001";
    public const string CommandCancellationDiagnosticId = "SYM002";
    public const string ConnectionCancellationDiagnosticId = "SYM003";
    public const string TypedResultsDiagnosticId = "SYM004";
    public const string ConstantSqlDiagnosticId = "SYM005";
    public const string ValidationOrderDiagnosticId = "SYM006";
    public const string SliceShapeDiagnosticId = "SYM007";

    private static readonly DiagnosticDescriptor TypedIdRule = CreateRule(
        TypedIdDiagnosticId,
        "Use the entity's typed identifier",
        "Identifier '{0}' must use {1} instead of Guid");

    private static readonly DiagnosticDescriptor CommandCancellationRule = CreateRule(
        CommandCancellationDiagnosticId,
        "Dapper commands must carry cancellation",
        "CommandDefinition must receive cancellationToken");

    private static readonly DiagnosticDescriptor ConnectionCancellationRule = CreateRule(
        ConnectionCancellationDiagnosticId,
        "Database connections must carry cancellation",
        "OpenConnectionAsync must receive a CancellationToken");

    private static readonly DiagnosticDescriptor TypedResultsRule = CreateRule(
        TypedResultsDiagnosticId,
        "Use typed Minimal API results",
        "Slice handlers must use TypedResults and a typed HttpResults return type");

    private static readonly DiagnosticDescriptor ConstantSqlRule = CreateRule(
        ConstantSqlDiagnosticId,
        "Dapper SQL must be compile-time constant",
        "CommandDefinition SQL must be a compile-time constant, never interpolated or concatenated");

    private static readonly DiagnosticDescriptor ValidationOrderRule = CreateRule(
        ValidationOrderDiagnosticId,
        "Validate before database I/O",
        "A handler with IValidator<T> must call ValidateAsync before opening a connection");

    private static readonly DiagnosticDescriptor SliceShapeRule = CreateRule(
        SliceShapeDiagnosticId,
        "Keep each request in one named slice",
        "Top-level slice '{0}' must be declared in '{0}.cs' and expose public static Map");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        TypedIdRule,
        CommandCancellationRule,
        ConnectionCancellationRule,
        TypedResultsRule,
        ConstantSqlRule,
        ValidationOrderRule,
        SliceShapeRule
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeIdentifier, SymbolKind.Property, SymbolKind.Parameter);
        context.RegisterSymbolAction(AnalyzeSliceShape, SymbolKind.NamedType);
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeHandler, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeIdentifier(SymbolAnalysisContext context)
    {
        var symbol = context.Symbol;
        if (!IsApiFeature(symbol) || symbol.Name is not ("Id" or "LanguageId" or "GreetingId"))
        {
            return;
        }

        var type = symbol switch
        {
            IPropertySymbol property => property.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null
        };

        if (type?.ToDisplayString() != "System.Guid")
        {
            return;
        }

        var expectedType = symbol.Name == "GreetingId" ? "GreetingId" : "LanguageId";
        if (symbol.Name == "Id")
        {
            expectedType = symbol.ContainingNamespace.ToDisplayString()
                .Contains(".Greetings", StringComparison.Ordinal)
                ? "GreetingId"
                : "LanguageId";
        }

        context.ReportDiagnostic(Diagnostic.Create(
            TypedIdRule,
            symbol.Locations.FirstOrDefault(),
            symbol.Name,
            expectedType));
    }

    private static void AnalyzeSliceShape(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol type
            || type.ContainingType is not null
            || !IsApiFeature(type)
            || type.Name.EndsWith("Feature", StringComparison.Ordinal))
        {
            return;
        }

        var sourceLocation = type.Locations.FirstOrDefault(location => location.IsInSource);
        var fileName = sourceLocation?.SourceTree is null
            ? null
            : Path.GetFileNameWithoutExtension(sourceLocation.SourceTree.FilePath);
        var hasMap = type.GetMembers("Map")
            .OfType<IMethodSymbol>()
            .Any(method =>
                method.DeclaredAccessibility == Accessibility.Public
                && method.IsStatic);

        if (fileName == type.Name && hasMap)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            SliceShapeRule,
            sourceLocation,
            type.Name));
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;
        var constructor = context.SemanticModel.GetSymbolInfo(creation, context.CancellationToken)
            .Symbol as IMethodSymbol;
        if (constructor?.ContainingType.ToDisplayString() != "Dapper.CommandDefinition")
        {
            return;
        }

        var arguments = creation.ArgumentList?.Arguments ?? default;
        if (!arguments.Any(argument =>
                argument.NameColon?.Name.Identifier.ValueText == "cancellationToken"))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                CommandCancellationRule,
                creation.GetLocation()));
        }

        var sqlArgument = arguments.FirstOrDefault();
        if (sqlArgument is null
            || !context.SemanticModel.GetConstantValue(
                sqlArgument.Expression,
                context.CancellationToken).HasValue)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ConstantSqlRule,
                sqlArgument?.GetLocation() ?? creation.GetLocation()));
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken)
            .Symbol as IMethodSymbol;
        if (method is null)
        {
            return;
        }

        if (context.ContainingSymbol is { } containingSymbol
            && IsApiFeature(containingSymbol)
            && method.ContainingType.ToDisplayString() == "System.Guid"
            && method.Name is "Parse" or "TryParse")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypedIdRule,
                invocation.GetLocation(),
                "parsed entity ID",
                "LanguageId or GreetingId"));
        }

        if (method.Name == "OpenConnectionAsync"
            && method.ContainingType.Name == "NpgsqlDataSource")
        {
            var hasCancellation = invocation.ArgumentList.Arguments.Any(argument =>
                context.SemanticModel.GetTypeInfo(
                    argument.Expression,
                    context.CancellationToken).ConvertedType?.ToDisplayString()
                == "System.Threading.CancellationToken");
            if (!hasCancellation)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ConnectionCancellationRule,
                    invocation.GetLocation()));
            }
        }

        if (method.ContainingType.ToDisplayString() == "Microsoft.AspNetCore.Http.Results")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypedResultsRule,
                invocation.GetLocation()));
        }
    }

    private static void AnalyzeHandler(SyntaxNodeAnalysisContext context)
    {
        var declaration = (MethodDeclarationSyntax)context.Node;
        if (declaration.Identifier.ValueText != "Handle"
            || context.SemanticModel.GetDeclaredSymbol(
                declaration,
                context.CancellationToken) is not IMethodSymbol method
            || !IsApiFeature(method))
        {
            return;
        }

        if (!IsTypedHttpResult(method.ReturnType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypedResultsRule,
                declaration.ReturnType.GetLocation()));
        }

        var hasValidator = method.Parameters.Any(parameter =>
            parameter.Type is INamedTypeSymbol named
            && named.OriginalDefinition.ToDisplayString() == "FluentValidation.IValidator<T>");
        if (!hasValidator)
        {
            return;
        }

        var invocations = declaration.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => new
            {
                Syntax = invocation,
                Method = context.SemanticModel.GetSymbolInfo(
                    invocation,
                    context.CancellationToken).Symbol as IMethodSymbol
            })
            .Where(item => item.Method is not null)
            .ToList();
        var validation = invocations.FirstOrDefault(item => item.Method!.Name == "ValidateAsync");
        var databaseIo = invocations.FirstOrDefault(item =>
            item.Method!.Name == "OpenConnectionAsync");

        if (validation is null
            || databaseIo is not null
            && validation.Syntax.SpanStart > databaseIo.Syntax.SpanStart)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                ValidationOrderRule,
                declaration.Identifier.GetLocation()));
        }
    }

    private static bool IsTypedHttpResult(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { Name: "Task", TypeArguments.Length: 1 } task)
        {
            type = task.TypeArguments[0];
        }

        return type.ContainingNamespace?.ToDisplayString()
            == "Microsoft.AspNetCore.Http.HttpResults";
    }

    private static bool IsApiFeature(ISymbol symbol) =>
        symbol.ContainingNamespace?.ToDisplayString()
            .StartsWith("SymphonyTest1.Api.Features.", StringComparison.Ordinal) == true;

    private static DiagnosticDescriptor CreateRule(
        string id,
        string title,
        string message) =>
        new(
            id,
            title,
            message,
            "Architecture",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Enforces the repository's request-oriented vertical-slice conventions.");
}
