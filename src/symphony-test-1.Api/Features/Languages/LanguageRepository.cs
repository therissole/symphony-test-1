using Dapper;
using SymphonyTest1.Api.Infrastructure;

namespace SymphonyTest1.Api.Features.Languages;

public interface ILanguageRepository
{
    Task<Language?> GetByIdAsync(Guid id);
    Task<Language?> GetByCodeAsync(string code);
    Task<IEnumerable<Language>> GetAllAsync();
    Task<Guid> CreateAsync(CreateLanguageRequest request);
    Task<bool> UpdateAsync(Guid id, UpdateLanguageRequest request);
    Task<bool> DeleteAsync(Guid id);
}

public class LanguageRepository : ILanguageRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<LanguageRepository> _logger;

    public LanguageRepository(IDbConnectionFactory connectionFactory, ILogger<LanguageRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Language?> GetByIdAsync(Guid id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = "SELECT id, name, code, created_at as CreatedAt, updated_at as UpdatedAt FROM languages WHERE id = @Id";
        return await connection.QuerySingleOrDefaultAsync<Language>(sql, new { Id = id });
    }

    public async Task<Language?> GetByCodeAsync(string code)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = "SELECT id, name, code, created_at as CreatedAt, updated_at as UpdatedAt FROM languages WHERE code = @Code";
        return await connection.QuerySingleOrDefaultAsync<Language>(sql, new { Code = code });
    }

    public async Task<IEnumerable<Language>> GetAllAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = "SELECT id, name, code, created_at as CreatedAt, updated_at as UpdatedAt FROM languages ORDER BY name";
        return await connection.QueryAsync<Language>(sql);
    }

    public async Task<Guid> CreateAsync(CreateLanguageRequest request)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            INSERT INTO languages (name, code)
            VALUES (@Name, @Code)
            RETURNING id";
        
        var id = await connection.ExecuteScalarAsync<Guid>(sql, request);
        _logger.LogInformation("Created language with id {LanguageId}", id);
        return id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateLanguageRequest request)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            UPDATE languages
            SET name = @Name, code = @Code, updated_at = CURRENT_TIMESTAMP
            WHERE id = @Id";
        
        var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, request.Name, request.Code });
        
        if (rowsAffected > 0)
        {
            _logger.LogInformation("Updated language with id {LanguageId}", id);
        }
        
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = "DELETE FROM languages WHERE id = @Id";
        
        var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
        
        if (rowsAffected > 0)
        {
            _logger.LogInformation("Deleted language with id {LanguageId}", id);
        }
        
        return rowsAffected > 0;
    }
}
