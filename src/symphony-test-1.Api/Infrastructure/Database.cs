using Npgsql;

namespace SymphonyTest1.Api.Infrastructure;

public static class Database
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is required.");

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        return services;
    }
}
