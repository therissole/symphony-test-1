using Dapper;
using SymphonyTest1.Api.Infrastructure;

namespace SymphonyTest1.Api.Features.Greetings;

public interface IGreetingRepository
{
    Task<Greeting?> GetByIdAsync(Guid id);
    Task<IEnumerable<Greeting>> GetAllAsync();
    Task<IEnumerable<Greeting>> GetByLanguageIdAsync(Guid languageId);
    Task<Greeting?> GetByLanguageCodeAsync(string languageCode, bool? formal = null);
    Task<Guid> CreateAsync(CreateGreetingRequest request);
    Task<bool> UpdateAsync(Guid id, UpdateGreetingRequest request);
    Task<bool> DeleteAsync(Guid id);
}

public class GreetingRepository : IGreetingRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<GreetingRepository> _logger;

    public GreetingRepository(IDbConnectionFactory connectionFactory, ILogger<GreetingRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Greeting?> GetByIdAsync(Guid id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            SELECT id, language_id as LanguageId, greeting_text as GreetingText, formal, 
                   created_at as CreatedAt, updated_at as UpdatedAt 
            FROM greetings 
            WHERE id = @Id";
        return await connection.QuerySingleOrDefaultAsync<Greeting>(sql, new { Id = id });
    }

    public async Task<IEnumerable<Greeting>> GetAllAsync()
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            SELECT id, language_id as LanguageId, greeting_text as GreetingText, formal, 
                   created_at as CreatedAt, updated_at as UpdatedAt 
            FROM greetings 
            ORDER BY greeting_text";
        return await connection.QueryAsync<Greeting>(sql);
    }

    public async Task<IEnumerable<Greeting>> GetByLanguageIdAsync(Guid languageId)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            SELECT id, language_id as LanguageId, greeting_text as GreetingText, formal, 
                   created_at as CreatedAt, updated_at as UpdatedAt 
            FROM greetings 
            WHERE language_id = @LanguageId
            ORDER BY greeting_text";
        return await connection.QueryAsync<Greeting>(sql, new { LanguageId = languageId });
    }

    public async Task<Greeting?> GetByLanguageCodeAsync(string languageCode, bool? formal = null)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        
        var sql = @"
            SELECT g.id, g.language_id as LanguageId, g.greeting_text as GreetingText, g.formal, 
                   g.created_at as CreatedAt, g.updated_at as UpdatedAt 
            FROM greetings g
            INNER JOIN languages l ON g.language_id = l.id
            WHERE l.code = @LanguageCode";

        if (formal.HasValue)
        {
            sql += " AND g.formal = @Formal";
        }
        
        sql += " ORDER BY g.formal LIMIT 1";

        return await connection.QuerySingleOrDefaultAsync<Greeting>(sql, new { LanguageCode = languageCode, Formal = formal });
    }

    public async Task<Guid> CreateAsync(CreateGreetingRequest request)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            INSERT INTO greetings (language_id, greeting_text, formal)
            VALUES (@LanguageId, @GreetingText, @Formal)
            RETURNING id";
        
        var id = await connection.ExecuteScalarAsync<Guid>(sql, request);
        _logger.LogInformation("Created greeting with id {GreetingId}", id);
        return id;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateGreetingRequest request)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = @"
            UPDATE greetings
            SET language_id = @LanguageId, greeting_text = @GreetingText, formal = @Formal, updated_at = CURRENT_TIMESTAMP
            WHERE id = @Id";
        
        var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id, request.LanguageId, request.GreetingText, request.Formal });
        
        if (rowsAffected > 0)
        {
            _logger.LogInformation("Updated greeting with id {GreetingId}", id);
        }
        
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var connection = await _connectionFactory.CreateConnectionAsync();
        const string sql = "DELETE FROM greetings WHERE id = @Id";
        
        var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
        
        if (rowsAffected > 0)
        {
            _logger.LogInformation("Deleted greeting with id {GreetingId}", id);
        }
        
        return rowsAffected > 0;
    }
}
