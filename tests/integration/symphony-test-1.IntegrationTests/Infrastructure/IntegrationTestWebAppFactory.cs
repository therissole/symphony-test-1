using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SymphonyTest1.Api.Infrastructure.Authorization;
using Testcontainers.PostgreSql;

namespace SymphonyTest1.IntegrationTests.Infrastructure;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly bool _authenticated;
    private readonly string _environment;
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("symphony_test_1_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public IntegrationTestWebAppFactory(
        string environment = "Testing",
        bool authenticated = true)
    {
        _environment = environment;
        _authenticated = authenticated;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<NpgsqlDataSource>();
            services.AddSingleton(_ => NpgsqlDataSource.Create(_dbContainer.GetConnectionString()));
            services.RemoveAll<IOpenFgaAuthorization>();
            services.AddSingleton<IOpenFgaAuthorization, AllowAllOpenFgaAuthorization>();
            services.AddSingleton(new TestAuthenticationState(_authenticated));
            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
        });

        builder.UseEnvironment(_environment);
    }

    public async Task StartAsync()
    {
        await _dbContainer.StartAsync();
        await ApplyMigrations();
    }

    private async Task ApplyMigrations()
    {
        var connectionString = _dbContainer.GetConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var currentDir = Directory.GetCurrentDirectory();
        var solutionRoot = FindSolutionRoot(currentDir);

        if (solutionRoot == null)
        {
            throw new InvalidOperationException("Could not find solution root directory");
        }

        var migrationsPath = Path.Combine(solutionRoot, "db", "migrations");

        foreach (var migrationPath in Directory.GetFiles(migrationsPath, "V*.sql").Order())
        {
            var migration = await File.ReadAllTextAsync(migrationPath);
#pragma warning disable CA2100 // Migration SQL is trusted, versioned repository content.
            await using var command = new NpgsqlCommand(migration, connection);
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync();
        }
    }

    private static string? FindSolutionRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            var hasSolution = dir.GetFiles("*.sln").Length > 0 || dir.GetFiles("*.slnx").Length > 0;
            var hasDbMigrations = dir.GetDirectories("db").Any(d =>
                Directory.Exists(Path.Combine(d.FullName, "migrations")));

            if (hasSolution && hasDbMigrations)
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        return null;
    }

    public async Task StopAsync()
    {
        await _dbContainer.DisposeAsync();
    }

    private sealed class AllowAllOpenFgaAuthorization : IOpenFgaAuthorization
    {
        public Task<bool> IsAllowedAsync(
            System.Security.Claims.ClaimsPrincipal user,
            string relation,
            string @object,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task WriteTupleAsync(
            string user,
            string relation,
            string @object,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteTupleAsync(
            string user,
            string relation,
            string @object,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

    }
}
