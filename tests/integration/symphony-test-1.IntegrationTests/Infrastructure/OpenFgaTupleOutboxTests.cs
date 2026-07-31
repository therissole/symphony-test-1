using System.Security.Claims;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SymphonyTest1.Api.Infrastructure.Authorization;

namespace SymphonyTest1.IntegrationTests.Infrastructure;

[TestFixture]
public sealed class OpenFgaTupleOutboxTests
{
    private IntegrationTestWebAppFactory _factory = null!;
    private NpgsqlDataSource _dataSource = null!;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new IntegrationTestWebAppFactory();
        await _factory.StartAsync();
        _dataSource = NpgsqlDataSource.Create(_factory.ConnectionString);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dataSource.DisposeAsync();
        await _factory.StopAsync();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task PendingTupleOperation_IsDurableAndRecoveredIdempotently()
    {
        var authorization = new RecoveringAuthorization();
        var outbox = new OpenFgaTupleOutbox(
            _dataSource,
            authorization,
            TimeProvider.System,
            NullLogger<OpenFgaTupleOutbox>.Instance);
        var operationId = await EnqueueCommittedOperationAsync(
            outbox,
            OpenFgaTupleOperation.Write,
            $"language:{Guid.NewGuid()}");

        Assert.That(
            async () => await outbox.DispatchAsync(operationId, CancellationToken.None),
            Throws.TypeOf<InvalidOperationException>());

        var failed = await ReadStateAsync(operationId);
        Assert.Multiple(() =>
        {
            Assert.That(failed.ProcessedAt, Is.Null);
            Assert.That(failed.AttemptCount, Is.EqualTo(1));
            Assert.That(failed.LastError, Is.EqualTo(nameof(InvalidOperationException)));
        });

        authorization.AllowWrites();
        await outbox.RecoverPendingAsync(CancellationToken.None);

        var recovered = await ReadStateAsync(operationId);
        Assert.Multiple(() =>
        {
            Assert.That(recovered.ProcessedAt, Is.Not.Null);
            Assert.That(recovered.AttemptCount, Is.EqualTo(2));
            Assert.That(recovered.LastError, Is.Null);
            Assert.That(authorization.WriteCount, Is.EqualTo(2));
        });

        await outbox.DispatchAsync(operationId, CancellationToken.None);
        Assert.That(authorization.WriteCount, Is.EqualTo(2), "Replaying a completed operation must be a no-op.");
        outbox.Dispose();
    }

    [Test]
    public async Task Dispatch_PreservesTupleOperationOrderAcrossWorkers()
    {
        var authorization = new RecoveringAuthorization();
        var outbox = new OpenFgaTupleOutbox(
            _dataSource,
            authorization,
            TimeProvider.System,
            NullLogger<OpenFgaTupleOutbox>.Instance);
        var tupleObject = $"language:{Guid.NewGuid()}";
        var writeId = await EnqueueCommittedOperationAsync(
            outbox,
            OpenFgaTupleOperation.Write,
            tupleObject);
        var deleteId = await EnqueueCommittedOperationAsync(
            outbox,
            OpenFgaTupleOperation.Delete,
            tupleObject);

        Assert.That(
            async () => await outbox.DispatchAsync(writeId, CancellationToken.None),
            Throws.TypeOf<InvalidOperationException>());

        authorization.AllowWrites();
        await outbox.DispatchAsync(deleteId, CancellationToken.None);

        var writeState = await ReadStateAsync(writeId);
        var deleteState = await ReadStateAsync(deleteId);
        Assert.Multiple(() =>
        {
            Assert.That(writeState.ProcessedAt, Is.Not.Null);
            Assert.That(deleteState.ProcessedAt, Is.Not.Null);
            Assert.That(authorization.WriteCount, Is.EqualTo(2));
            Assert.That(authorization.DeleteCount, Is.EqualTo(1));
        });
        outbox.Dispose();
    }

    [Test]
    public async Task Dispatch_WaitsForAnInFlightWorkerBeforeReportingCompletion()
    {
        var authorization = new BlockingAuthorization();
        var firstWorker = new OpenFgaTupleOutbox(
            _dataSource,
            authorization,
            TimeProvider.System,
            NullLogger<OpenFgaTupleOutbox>.Instance);
        var secondWorker = new OpenFgaTupleOutbox(
            _dataSource,
            authorization,
            TimeProvider.System,
            NullLogger<OpenFgaTupleOutbox>.Instance);
        var operationId = await EnqueueCommittedOperationAsync(
            firstWorker,
            OpenFgaTupleOperation.Write,
            $"language:{Guid.NewGuid()}");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var firstDispatch = firstWorker.DispatchAsync(operationId, timeout.Token);
        Task? secondDispatch = null;

        try
        {
            await authorization.WriteStarted.WaitAsync(timeout.Token);
            secondDispatch = secondWorker.DispatchAsync(operationId, timeout.Token);
            await WaitForBlockedDispatchAsync(timeout.Token);

            Assert.That(
                secondDispatch.IsCompleted,
                Is.False,
                "A competing direct dispatch must wait until the tuple operation is durably complete.");

            authorization.ReleaseWrite();
            await Task.WhenAll(firstDispatch, secondDispatch).WaitAsync(timeout.Token);

            var state = await ReadStateAsync(operationId);
            Assert.Multiple(() =>
            {
                Assert.That(state.ProcessedAt, Is.Not.Null);
                Assert.That(authorization.WriteCount, Is.EqualTo(1));
            });
        }
        finally
        {
            authorization.ReleaseWrite();
            await firstDispatch;
            if (secondDispatch is not null)
            {
                await secondDispatch;
            }

            firstWorker.Dispose();
            secondWorker.Dispose();
        }
    }

    [Test]
    public async Task Enqueue_SerializesUncommittedOperationsForTheSameTuple()
    {
        var authorization = new RecoveringAuthorization();
        var outbox = new OpenFgaTupleOutbox(
            _dataSource,
            authorization,
            TimeProvider.System,
            NullLogger<OpenFgaTupleOutbox>.Instance);
        var tupleObject = $"language:{Guid.NewGuid()}";
        await using var firstConnection = await _dataSource.OpenConnectionAsync();
        await using var secondConnection = await _dataSource.OpenConnectionAsync();
        await using var firstTransaction = await firstConnection.BeginTransactionAsync();
        await using var secondTransaction = await secondConnection.BeginTransactionAsync();
        var firstCommitted = false;
        var secondCommitted = false;
        Task<Guid>? secondEnqueue = null;

        try
        {
            var firstId = await outbox.EnqueueAsync(
                OpenFgaTupleOperation.Write,
                "system:global",
                "system",
                tupleObject,
                firstConnection,
                firstTransaction,
                CancellationToken.None);
            secondEnqueue = outbox.EnqueueAsync(
                OpenFgaTupleOperation.Delete,
                "system:global",
                "system",
                tupleObject,
                secondConnection,
                secondTransaction,
                CancellationToken.None);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await WaitForBlockedCommandAsync("pg_advisory_xact_lock", timeout.Token);

            Assert.That(
                secondEnqueue.IsCompleted,
                Is.False,
                "A later transaction must not allocate tuple order before the earlier transaction commits.");

            await firstTransaction.CommitAsync(timeout.Token);
            firstCommitted = true;
            var secondId = await secondEnqueue.WaitAsync(timeout.Token);
            await secondTransaction.CommitAsync(timeout.Token);
            secondCommitted = true;

            authorization.AllowWrites();
            await outbox.DispatchAsync(secondId, timeout.Token);
            var firstState = await ReadStateAsync(firstId);
            var secondState = await ReadStateAsync(secondId);
            Assert.Multiple(() =>
            {
                Assert.That(firstState.SequenceNumber, Is.LessThan(secondState.SequenceNumber));
                Assert.That(firstState.ProcessedAt, Is.Not.Null);
                Assert.That(secondState.ProcessedAt, Is.Not.Null);
                Assert.That(authorization.WriteCount, Is.EqualTo(1));
                Assert.That(authorization.DeleteCount, Is.EqualTo(1));
            });
        }
        finally
        {
            if (!firstCommitted)
            {
                await firstTransaction.RollbackAsync();
            }

            if (secondEnqueue is not null)
            {
                await secondEnqueue;
            }

            if (!secondCommitted)
            {
                await secondTransaction.RollbackAsync();
            }

            outbox.Dispose();
        }
    }

    [Test]
    public async Task Recovery_AttemptsAPoisonedTupleHeadOnlyOnceAndContinuesWithOtherTuples()
    {
        var poisonObject = $"language:{Guid.NewGuid()}";
        var healthyObject = $"language:{Guid.NewGuid()}";
        var authorization = new SelectivelyFailingAuthorization(poisonObject);
        var outbox = new OpenFgaTupleOutbox(
            _dataSource,
            authorization,
            TimeProvider.System,
            NullLogger<OpenFgaTupleOutbox>.Instance);
        var poisonHeadId = await EnqueueCommittedOperationAsync(
            outbox,
            OpenFgaTupleOperation.Write,
            poisonObject);
        var poisonDeleteId = await EnqueueCommittedOperationAsync(
            outbox,
            OpenFgaTupleOperation.Delete,
            poisonObject);
        var poisonTailId = await EnqueueCommittedOperationAsync(
            outbox,
            OpenFgaTupleOperation.Write,
            poisonObject);
        var healthyId = await EnqueueCommittedOperationAsync(
            outbox,
            OpenFgaTupleOperation.Write,
            healthyObject);

        await outbox.RecoverPendingAsync(CancellationToken.None);

        var poisonHead = await ReadStateAsync(poisonHeadId);
        var poisonDelete = await ReadStateAsync(poisonDeleteId);
        var poisonTail = await ReadStateAsync(poisonTailId);
        var healthy = await ReadStateAsync(healthyId);
        Assert.Multiple(() =>
        {
            Assert.That(poisonHead.AttemptCount, Is.EqualTo(1));
            Assert.That(poisonHead.ProcessedAt, Is.Null);
            Assert.That(poisonDelete.AttemptCount, Is.Zero);
            Assert.That(poisonTail.AttemptCount, Is.Zero);
            Assert.That(healthy.ProcessedAt, Is.Not.Null);
            Assert.That(authorization.WriteAttempts(poisonObject), Is.EqualTo(1));
            Assert.That(authorization.WriteAttempts(healthyObject), Is.EqualTo(1));
            Assert.That(authorization.DeleteCount, Is.Zero);
        });
        outbox.Dispose();
    }

    [Test]
    public async Task Pruning_RemovesOneBoundedBatchOfExpiredOperationsAndRetainsRecentAndPendingRows()
    {
        const int pruneBatchSize = 1_000;
        var now = new DateTimeOffset(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var authorization = new RecoveringAuthorization();
        var outbox = new OpenFgaTupleOutbox(
            _dataSource,
            authorization,
            new FixedTimeProvider(now),
            NullLogger<OpenFgaTupleOutbox>.Instance);
        const string insertExpiredSql = """
            INSERT INTO openfga_tuple_outbox (
                id,
                operation,
                tuple_user,
                tuple_relation,
                tuple_object,
                created_at,
                processed_at)
            SELECT
                md5('expired-' || series.value)::uuid,
                'write',
                'system:global',
                'system',
                'language:expired-' || series.value,
                @CreatedAt,
                @ProcessedAt
            FROM generate_series(1, @Count) AS series(value)
            """;
        const string insertRetainedSql = """
            INSERT INTO openfga_tuple_outbox (
                id,
                operation,
                tuple_user,
                tuple_relation,
                tuple_object,
                created_at,
                processed_at)
            VALUES
                (@RecentId, 'write', 'system:global', 'system', 'language:recent', @RecentCreatedAt, @RecentProcessedAt),
                (@PendingId, 'delete', 'system:global', 'system', 'language:pending', @PendingCreatedAt, NULL)
            """;

        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await connection.ExecuteAsync(
                insertExpiredSql,
                new
                {
                    CreatedAt = now.AddDays(-9),
                    ProcessedAt = now.AddDays(-8),
                    Count = pruneBatchSize + 1
                });
            await connection.ExecuteAsync(
                insertRetainedSql,
                new
                {
                    RecentId = Guid.NewGuid(),
                    PendingId = Guid.NewGuid(),
                    RecentCreatedAt = now.AddDays(-2),
                    RecentProcessedAt = now.AddDays(-1),
                    PendingCreatedAt = now.AddDays(-30)
                });
        }

        await outbox.PruneProcessedAsync(CancellationToken.None);

        var stateAfterFirstBatch = await ReadRetentionStateAsync();
        Assert.Multiple(() =>
        {
            Assert.That(
                stateAfterFirstBatch.ExpiredCount,
                Is.EqualTo(1),
                "A maintenance pass must delete no more than its bounded batch.");
            Assert.That(stateAfterFirstBatch.RecentCount, Is.EqualTo(1));
            Assert.That(stateAfterFirstBatch.PendingCount, Is.EqualTo(1));
        });

        await outbox.PruneProcessedAsync(CancellationToken.None);

        var finalState = await ReadRetentionStateAsync();
        Assert.Multiple(() =>
        {
            Assert.That(finalState.ExpiredCount, Is.Zero);
            Assert.That(finalState.RecentCount, Is.EqualTo(1));
            Assert.That(finalState.PendingCount, Is.EqualTo(1));
        });
        outbox.Dispose();
    }

    private async Task<Guid> EnqueueCommittedOperationAsync(
        IOpenFgaTupleOutbox outbox,
        OpenFgaTupleOperation operation,
        string @object)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var operationId = await outbox.EnqueueAsync(
            operation,
            "system:global",
            "system",
            @object,
            connection,
            transaction,
            CancellationToken.None);
        await transaction.CommitAsync();
        return operationId;
    }

    private async Task<OutboxState> ReadStateAsync(Guid operationId)
    {
        const string sql = """
            SELECT
                sequence_number AS SequenceNumber,
                processed_at AS ProcessedAt,
                attempt_count AS AttemptCount,
                last_error AS LastError
            FROM openfga_tuple_outbox
            WHERE id = @Id
            """;
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QuerySingleAsync<OutboxState>(sql, new { Id = operationId });
    }

    private async Task<RetentionState> ReadRetentionStateAsync()
    {
        const string sql = """
            SELECT
                COUNT(*) FILTER (
                    WHERE tuple_object LIKE 'language:expired-%') AS ExpiredCount,
                COUNT(*) FILTER (
                    WHERE tuple_object = 'language:recent') AS RecentCount,
                COUNT(*) FILTER (
                    WHERE tuple_object = 'language:pending'
                      AND processed_at IS NULL) AS PendingCount
            FROM openfga_tuple_outbox
            """;
        await using var connection = await _dataSource.OpenConnectionAsync();
        return await connection.QuerySingleAsync<RetentionState>(sql);
    }

    private Task WaitForBlockedDispatchAsync(CancellationToken cancellationToken) =>
        WaitForBlockedCommandAsync("openfga_tuple_outbox AS target_operation", cancellationToken);

    private async Task WaitForBlockedCommandAsync(
        string commandFragment,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_stat_activity
                WHERE datname = current_database()
                  AND pid <> pg_backend_pid()
                  AND wait_event_type = 'Lock'
                  AND query LIKE @Pattern)
            """;
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(25), TimeProvider.System);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var blocked = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                sql,
                new { Pattern = $"%{commandFragment}%" },
                cancellationToken: cancellationToken));
            if (blocked)
            {
                return;
            }
        }

        throw new InvalidOperationException("The competing outbox dispatch did not wait on the row lock.");
    }

    private sealed record OutboxState(
        long SequenceNumber,
        DateTime? ProcessedAt,
        int AttemptCount,
        string? LastError);

    private sealed record RetentionState(
        long ExpiredCount,
        long RecentCount,
        long PendingCount);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecoveringAuthorization : IOpenFgaAuthorization
    {
        private bool _allowWrites;

        public int WriteCount { get; private set; }
        public int DeleteCount { get; private set; }

        public void AllowWrites() => _allowWrites = true;

        public Task<bool> IsAllowedAsync(
            ClaimsPrincipal user,
            string relation,
            string @object,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task WriteTupleAsync(
            string user,
            string relation,
            string @object,
            CancellationToken cancellationToken)
        {
            WriteCount++;
            if (!_allowWrites)
            {
                throw new InvalidOperationException("Simulated OpenFGA failure.");
            }

            return Task.CompletedTask;
        }

        public Task DeleteTupleAsync(
            string user,
            string relation,
            string @object,
            CancellationToken cancellationToken)
        {
            DeleteCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListObjectsAsync(
            ClaimsPrincipal user,
            string relation,
            string type,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class BlockingAuthorization : IOpenFgaAuthorization
    {
        private readonly TaskCompletionSource<bool> _writeStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseWrite =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _writeCount;

        public Task WriteStarted => _writeStarted.Task;
        public int WriteCount => Volatile.Read(ref _writeCount);

        public void ReleaseWrite() => _releaseWrite.TrySetResult(true);

        public Task<bool> IsAllowedAsync(
            ClaimsPrincipal user,
            string relation,
            string @object,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public async Task WriteTupleAsync(
            string user,
            string relation,
            string @object,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _writeCount);
            _writeStarted.TrySetResult(true);
            await _releaseWrite.Task.WaitAsync(cancellationToken);
        }

        public Task DeleteTupleAsync(
            string user,
            string relation,
            string @object,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> ListObjectsAsync(
            ClaimsPrincipal user,
            string relation,
            string type,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class SelectivelyFailingAuthorization(string poisonObject) : IOpenFgaAuthorization
    {
        private readonly Dictionary<string, int> _writeAttempts = [];

        public int DeleteCount { get; private set; }

        public int WriteAttempts(string @object) =>
            _writeAttempts.GetValueOrDefault(@object);

        public Task<bool> IsAllowedAsync(
            ClaimsPrincipal user,
            string relation,
            string @object,
            CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task WriteTupleAsync(
            string user,
            string relation,
            string @object,
            CancellationToken cancellationToken)
        {
            _writeAttempts[@object] = WriteAttempts(@object) + 1;
            if (string.Equals(@object, poisonObject, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Simulated tuple-specific OpenFGA failure.");
            }

            return Task.CompletedTask;
        }

        public Task DeleteTupleAsync(
            string user,
            string relation,
            string @object,
            CancellationToken cancellationToken)
        {
            DeleteCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListObjectsAsync(
            ClaimsPrincipal user,
            string relation,
            string type,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }
}
