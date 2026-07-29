using System.Security.Cryptography;
using Npgsql;
using NpgsqlTypes;

const string connectionStringName = "DefaultConnection";
const string migrationLockName = "symphony-test-1-database-migrations";

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationSource.Cancel();
};

var cancellationToken = cancellationSource.Token;
var timeProvider = TimeProvider.System;
var connectionString =
    Environment.GetEnvironmentVariable($"ConnectionStrings__{connectionStringName}")
    ?? throw new InvalidOperationException(
        $"Connection string '{connectionStringName}' is required.");

var migrationsDirectory = Path.Combine(AppContext.BaseDirectory, "Migrations");
var migrationPaths = Directory
    .EnumerateFiles(migrationsDirectory, "V*.sql")
    .OrderBy(Path.GetFileName, StringComparer.Ordinal)
    .ToArray();

if (migrationPaths.Length == 0)
{
    throw new InvalidOperationException(
        $"No database migrations were found in '{migrationsDirectory}'.");
}

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync(cancellationToken);

await using (var lockCommand = new NpgsqlCommand(
    "SELECT pg_advisory_lock(hashtext(@lockName));",
    connection))
{
    lockCommand.Parameters.AddWithValue("lockName", migrationLockName);
    await lockCommand.ExecuteNonQueryAsync(cancellationToken);
}

await using (var historyCommand = new NpgsqlCommand(
    """
    CREATE TABLE IF NOT EXISTS __schema_migrations
    (
        version TEXT PRIMARY KEY,
        checksum TEXT NOT NULL,
        applied_at TIMESTAMPTZ NOT NULL
    );
    """,
    connection))
{
    await historyCommand.ExecuteNonQueryAsync(cancellationToken);
}

foreach (var migrationPath in migrationPaths)
{
    var version = Path.GetFileNameWithoutExtension(migrationPath);
    var migrationBytes = await File.ReadAllBytesAsync(migrationPath, cancellationToken);
    var checksum = Convert.ToHexString(SHA256.HashData(migrationBytes));

    await using var lookupCommand = new NpgsqlCommand(
        "SELECT checksum FROM __schema_migrations WHERE version = @version;",
        connection);
    lookupCommand.Parameters.AddWithValue("version", version);

    var appliedChecksum =
        (string?)await lookupCommand.ExecuteScalarAsync(cancellationToken);

    if (appliedChecksum is not null)
    {
        if (!string.Equals(appliedChecksum, checksum, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Applied migration '{version}' does not match its recorded checksum.");
        }

        Console.WriteLine("Migration {0} is already applied.", version);
        continue;
    }

    await using var transaction =
        await connection.BeginTransactionAsync(cancellationToken);

    var migrationSql = System.Text.Encoding.UTF8.GetString(migrationBytes);
#pragma warning disable CA2100 // Migration SQL is trusted, versioned repository content.
    await using (var migrationCommand = new NpgsqlCommand(
        migrationSql,
        connection,
        transaction))
#pragma warning restore CA2100
    {
        await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    await using (var recordCommand = new NpgsqlCommand(
        """
        INSERT INTO __schema_migrations (version, checksum, applied_at)
        VALUES (@version, @checksum, @appliedAt);
        """,
        connection,
        transaction))
    {
        recordCommand.Parameters.AddWithValue("version", version);
        recordCommand.Parameters.Add(
            "checksum",
            NpgsqlDbType.Text).Value = checksum;
        recordCommand.Parameters.AddWithValue("appliedAt", timeProvider.GetUtcNow());
        await recordCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    await transaction.CommitAsync(cancellationToken);
    Console.WriteLine("Applied migration {0}.", version);
}

Console.WriteLine("Database is up to date.");
