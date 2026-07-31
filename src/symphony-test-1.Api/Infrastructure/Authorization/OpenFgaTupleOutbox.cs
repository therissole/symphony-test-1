using Dapper;
using Npgsql;

namespace SymphonyTest1.Api.Infrastructure.Authorization;

internal enum OpenFgaTupleOperation
{
    Write,
    Delete
}

internal interface IOpenFgaTupleOutbox
{
    Task<Guid> EnqueueAsync(
        OpenFgaTupleOperation operation,
        string user,
        string relation,
        string @object,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken);

    Task DispatchAsync(Guid operationId, CancellationToken cancellationToken);
}

/// <summary>
/// Durably coordinates application rows and OpenFGA tuples. Slices record tuple intent in their
/// database transaction, then synchronously dispatch it; the hosted loop recovers interrupted work.
/// </summary>
internal sealed partial class OpenFgaTupleOutbox : BackgroundService, IOpenFgaTupleOutbox
{
    private static readonly TimeSpan RecoveryInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ProcessedOperationRetention = TimeSpan.FromDays(7);
    private const int RecoveryTupleBatchSize = 100;
    private const int PruneBatchSize = 1_000;

    private readonly NpgsqlDataSource _dataSource;
    private readonly IOpenFgaAuthorization _authorization;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OpenFgaTupleOutbox> _logger;

    public OpenFgaTupleOutbox(
        NpgsqlDataSource dataSource,
        IOpenFgaAuthorization authorization,
        TimeProvider timeProvider,
        ILogger<OpenFgaTupleOutbox> logger)
    {
        _dataSource = dataSource;
        _authorization = authorization;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Guid> EnqueueAsync(
        OpenFgaTupleOperation operation,
        string user,
        string relation,
        string @object,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        const string lockSql = """
            SELECT pg_advisory_xact_lock(hashtextextended(@TupleKey, 0))
            """;
        const string sql = """
            INSERT INTO openfga_tuple_outbox (
                id,
                operation,
                tuple_user,
                tuple_relation,
                tuple_object,
                created_at)
            VALUES (
                @Id,
                @Operation,
                @User,
                @Relation,
                @Object,
                @CreatedAt)
            """;

        var operationId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            lockSql,
            new { TupleKey = $"{user}\n{relation}\n{@object}" },
            transaction: transaction,
            cancellationToken: cancellationToken));
        var command = new CommandDefinition(
            sql,
            new
            {
                Id = operationId,
                Operation = ToDatabaseValue(operation),
                User = user,
                Relation = relation,
                Object = @object,
                CreatedAt = _timeProvider.GetUtcNow()
            },
            transaction: transaction,
            cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);

        LogOperationEnqueued(_logger, operationId, operation, user, relation, @object);
        return operationId;
    }

    public async Task DispatchAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        const string selectSql = """
            SELECT
                pending_operation.id,
                pending_operation.sequence_number AS SequenceNumber,
                pending_operation.operation,
                pending_operation.tuple_user AS User,
                pending_operation.tuple_relation AS Relation,
                pending_operation.tuple_object AS Object
            FROM openfga_tuple_outbox AS target_operation
            JOIN LATERAL (
                SELECT
                    candidate_operation.id,
                    candidate_operation.sequence_number,
                    candidate_operation.operation,
                    candidate_operation.tuple_user,
                    candidate_operation.tuple_relation,
                    candidate_operation.tuple_object
                FROM openfga_tuple_outbox AS candidate_operation
                WHERE candidate_operation.processed_at IS NULL
                  AND candidate_operation.tuple_user = target_operation.tuple_user
                  AND candidate_operation.tuple_relation = target_operation.tuple_relation
                  AND candidate_operation.tuple_object = target_operation.tuple_object
                  AND candidate_operation.sequence_number <= target_operation.sequence_number
                ORDER BY candidate_operation.sequence_number
                LIMIT 1
                FOR UPDATE) AS pending_operation ON TRUE
            WHERE target_operation.id = @Id
            """;
        const string completeSql = """
            UPDATE openfga_tuple_outbox
            SET
                processed_at = @ProcessedAt,
                attempt_count = attempt_count + 1,
                last_error = NULL
            WHERE id = @Id
            """;
        const string failSql = """
            UPDATE openfga_tuple_outbox
            SET
                attempt_count = attempt_count + 1,
                last_error = @ErrorType
            WHERE id = @Id
            """;

        // Drain predecessors for this tuple in order. The row lock deliberately waits for a
        // competing worker so request dispatch cannot mistake "in flight" for "complete".
        while (true)
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            var operation = await connection.QuerySingleOrDefaultAsync<PendingOperation>(
                new CommandDefinition(
                    selectSql,
                    new { Id = operationId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            if (operation is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            try
            {
                await ApplyAsync(operation, cancellationToken);
                await connection.ExecuteAsync(new CommandDefinition(
                    completeSql,
                    new
                    {
                        Id = operation.Id,
                        ProcessedAt = _timeProvider.GetUtcNow()
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
                await transaction.CommitAsync(cancellationToken);
                LogOperationDispatched(_logger, operation.Id, operation.Operation);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    failSql,
                    new
                    {
                        Id = operation.Id,
                        ErrorType = exception.GetType().Name
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
                await transaction.CommitAsync(cancellationToken);
                LogOperationDispatchFailed(_logger, exception, operation.Id, operation.Operation);
                throw;
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverPendingAsync(stoppingToken);
        await PruneProcessedAsync(stoppingToken);

        using var timer = new PeriodicTimer(RecoveryInterval, _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RecoverPendingAsync(stoppingToken);
            await PruneProcessedAsync(stoppingToken);
        }
    }

    internal async Task RecoverPendingAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                (ARRAY_AGG(
                    pending_operation.id
                    ORDER BY pending_operation.sequence_number DESC))[1]
            FROM openfga_tuple_outbox AS pending_operation
            WHERE pending_operation.processed_at IS NULL
            GROUP BY
                pending_operation.tuple_user,
                pending_operation.tuple_relation,
                pending_operation.tuple_object
            ORDER BY
                (ARRAY_AGG(
                    pending_operation.attempt_count
                    ORDER BY pending_operation.sequence_number))[1],
                MIN(pending_operation.sequence_number)
            LIMIT @BatchSize
            """;

        IReadOnlyList<Guid> operationIds;
        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            operationIds = (await connection.QueryAsync<Guid>(new CommandDefinition(
                sql,
                new { BatchSize = RecoveryTupleBatchSize },
                cancellationToken: cancellationToken))).AsList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // Design-time OpenAPI generation starts the host without running application migrations.
            LogOutboxNotReady(_logger);
            return;
        }
        catch (Exception exception)
        {
            LogRecoveryScanFailed(_logger, exception);
            return;
        }

        foreach (var operationId in operationIds)
        {
            try
            {
                await DispatchAsync(operationId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // DispatchAsync records and logs the failure. Continue so one tuple cannot block the batch.
            }
        }
    }

    internal async Task PruneProcessedAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            WITH expired_operation AS (
                SELECT
                    completed_operation.id
                FROM openfga_tuple_outbox AS completed_operation
                WHERE completed_operation.processed_at < @ProcessedBefore
                ORDER BY
                    completed_operation.processed_at,
                    completed_operation.sequence_number
                LIMIT @BatchSize)
            DELETE FROM openfga_tuple_outbox AS completed_operation
            USING expired_operation
            WHERE completed_operation.id = expired_operation.id
            """;

        try
        {
            await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
            var deletedCount = await connection.ExecuteAsync(new CommandDefinition(
                sql,
                new
                {
                    ProcessedBefore = _timeProvider.GetUtcNow() - ProcessedOperationRetention,
                    BatchSize = PruneBatchSize
                },
                cancellationToken: cancellationToken));
            if (deletedCount > 0)
            {
                LogProcessedOperationsPruned(_logger, deletedCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (PostgresException exception)
            when (exception.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // Design-time OpenAPI generation starts the host without running application migrations.
            LogOutboxNotReady(_logger);
        }
        catch (Exception exception)
        {
            LogPruneFailed(_logger, exception);
        }
    }

    private Task ApplyAsync(
        PendingOperation operation,
        CancellationToken cancellationToken) =>
        operation.Operation switch
        {
            "write" => _authorization.WriteTupleAsync(
                operation.User,
                operation.Relation,
                operation.Object,
                cancellationToken),
            "delete" => _authorization.DeleteTupleAsync(
                operation.User,
                operation.Relation,
                operation.Object,
                cancellationToken),
            _ => throw new InvalidOperationException(
                $"Unsupported OpenFGA tuple outbox operation '{operation.Operation}'.")
        };

    private static string ToDatabaseValue(OpenFgaTupleOperation operation) =>
        operation switch
        {
            OpenFgaTupleOperation.Write => "write",
            OpenFgaTupleOperation.Delete => "delete",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

    private sealed record PendingOperation(
        Guid Id,
        long SequenceNumber,
        string Operation,
        string User,
        string Relation,
        string Object);

    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Debug,
        Message = "Enqueued OpenFGA tuple operation {OperationId}: {Operation} {User}#{Relation}@{Object}")]
    private static partial void LogOperationEnqueued(
        ILogger logger,
        Guid operationId,
        OpenFgaTupleOperation operation,
        string user,
        string relation,
        string @object);

    [LoggerMessage(
        EventId = 3102,
        Level = LogLevel.Debug,
        Message = "Dispatched OpenFGA tuple operation {OperationId} ({Operation})")]
    private static partial void LogOperationDispatched(
        ILogger logger,
        Guid operationId,
        string operation);

    [LoggerMessage(
        EventId = 3103,
        Level = LogLevel.Warning,
        Message = "OpenFGA tuple operation {OperationId} ({Operation}) failed and remains pending")]
    private static partial void LogOperationDispatchFailed(
        ILogger logger,
        Exception exception,
        Guid operationId,
        string operation);

    [LoggerMessage(
        EventId = 3104,
        Level = LogLevel.Warning,
        Message = "Could not scan the OpenFGA tuple outbox; recovery will retry")]
    private static partial void LogRecoveryScanFailed(
        ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 3105,
        Level = LogLevel.Debug,
        Message = "The OpenFGA tuple outbox is not available yet")]
    private static partial void LogOutboxNotReady(ILogger logger);

    [LoggerMessage(
        EventId = 3106,
        Level = LogLevel.Debug,
        Message = "Pruned {DeletedCount} processed OpenFGA tuple outbox operations")]
    private static partial void LogProcessedOperationsPruned(
        ILogger logger,
        int deletedCount);

    [LoggerMessage(
        EventId = 3107,
        Level = LogLevel.Warning,
        Message = "Could not prune the OpenFGA tuple outbox; maintenance will retry")]
    private static partial void LogPruneFailed(
        ILogger logger,
        Exception exception);
}
