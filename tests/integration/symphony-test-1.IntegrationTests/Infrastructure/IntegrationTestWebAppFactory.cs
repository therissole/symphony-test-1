using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SymphonyTest1.Api.Infrastructure;
using Testcontainers.PostgreSql;

namespace SymphonyTest1.IntegrationTests.Infrastructure;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("symphony_test_1_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing database connection factory
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbConnectionFactory));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add test database connection factory
            services.AddSingleton<IDbConnectionFactory>(
                new NpgsqlConnectionFactory(_dbContainer.GetConnectionString()));
        });

        builder.UseEnvironment("Testing");
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

        // Find the solution root directory
        var currentDir = Directory.GetCurrentDirectory();
        var solutionRoot = FindSolutionRoot(currentDir);
        
        if (solutionRoot == null)
        {
            throw new InvalidOperationException("Could not find solution root directory");
        }

        var migrationsPath = Path.Combine(solutionRoot, "db", "migrations");
        
        // Apply migrations from SQL files
        var migration1Path = Path.Combine(migrationsPath, "V1__create_languages_table.sql");
        var migration1 = await File.ReadAllTextAsync(migration1Path);
        await using var cmd1 = new NpgsqlCommand(migration1, connection);
        await cmd1.ExecuteNonQueryAsync();

        var migration2Path = Path.Combine(migrationsPath, "V2__create_greetings_table.sql");
        var migration2 = await File.ReadAllTextAsync(migration2Path);
        await using var cmd2 = new NpgsqlCommand(migration2, connection);
        await cmd2.ExecuteNonQueryAsync();
    }

    private string? FindSolutionRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            // Look for solution file (.sln or .slnx) and db/migrations directory
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
}
