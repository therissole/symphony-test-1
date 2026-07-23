using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
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
            services.RemoveAll<NpgsqlDataSource>();
            services.AddSingleton(_ => NpgsqlDataSource.Create(_dbContainer.GetConnectionString()));
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
            await using var command = new NpgsqlCommand(migration, connection);
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
}
