using System.Security.Claims;

using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Configuration;
using OpenFga.Sdk.Model;

namespace SymphonyTest1.Api.Infrastructure.Authorization;

internal interface IOpenFgaAuthorization
{
    Task<bool> IsAllowedAsync(
        ClaimsPrincipal user,
        string relation,
        string @object,
        CancellationToken cancellationToken);

    Task WriteTupleAsync(
        string user,
        string relation,
        string @object,
        CancellationToken cancellationToken);

    Task DeleteTupleAsync(
        string user,
        string relation,
        string @object,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListObjectsAsync(
        ClaimsPrincipal user,
        string relation,
        string type,
        CancellationToken cancellationToken);
}

internal sealed partial class OpenFgaAuthorization : IOpenFgaAuthorization, IDisposable
{
    private readonly string _apiUrl;
    private readonly string _storeName;
    private readonly ILogger<OpenFgaAuthorization> _logger;
    private readonly RetryableAsyncValue<OpenFgaClient> _storeClient;

    public OpenFgaAuthorization(
        IConfiguration configuration,
        ILogger<OpenFgaAuthorization> logger)
    {
        _apiUrl = configuration["OpenFga:ApiUrl"]
            ?? throw new InvalidOperationException("OpenFga:ApiUrl is required.");
        _storeName = configuration["OpenFga:StoreName"]
            ?? throw new InvalidOperationException("OpenFga:StoreName is required.");
        _logger = logger;
        _storeClient = new RetryableAsyncValue<OpenFgaClient>(CreateStoreClientAsync);
    }

    public async Task<bool> IsAllowedAsync(
        ClaimsPrincipal user,
        string relation,
        string @object,
        CancellationToken cancellationToken)
    {
        var subject = GetSubject(user);

        var client = await _storeClient.GetValueAsync(cancellationToken);
        var check = await client.Check(
            new ClientCheckRequest
            {
                User = $"user:{subject}",
                Relation = relation,
                Object = @object
            },
            CreateCheckOptions(),
            cancellationToken: cancellationToken);

        var allowed = check.Allowed == true;
        LogPermissionChecked(_logger, subject, relation, @object, allowed);
        return allowed;
    }

    public async Task WriteTupleAsync(
        string user,
        string relation,
        string @object,
        CancellationToken cancellationToken)
    {
        var client = await _storeClient.GetValueAsync(cancellationToken);
        await client.Write(
            new ClientWriteRequest
            {
                Writes =
                [
                    new ClientTupleKey
                    {
                        User = user,
                        Relation = relation,
                        Object = @object
                    }
                ]
            },
            new ClientWriteOptions
            {
                Conflict = new ConflictOptions
                {
                    OnDuplicateWrites = OnDuplicateWrites.Ignore
                }
            },
            cancellationToken: cancellationToken);

        LogTupleWritten(_logger, user, relation, @object);
    }

    public async Task DeleteTupleAsync(
        string user,
        string relation,
        string @object,
        CancellationToken cancellationToken)
    {
        var client = await _storeClient.GetValueAsync(cancellationToken);
        await client.Write(
            new ClientWriteRequest
            {
                Deletes =
                [
                    new ClientTupleKeyWithoutCondition
                    {
                        User = user,
                        Relation = relation,
                        Object = @object
                    }
                ]
            },
            new ClientWriteOptions
            {
                Conflict = new ConflictOptions
                {
                    OnMissingDeletes = OnMissingDeletes.Ignore
                }
            },
            cancellationToken: cancellationToken);

        LogTupleDeleted(_logger, user, relation, @object);
    }

    public async Task<IReadOnlyList<string>> ListObjectsAsync(
        ClaimsPrincipal user,
        string relation,
        string type,
        CancellationToken cancellationToken)
    {
        var subject = GetSubject(user);
        var client = await _storeClient.GetValueAsync(cancellationToken);
        var objects = new List<string>();
        await foreach (var response in client.StreamedListObjects(
            new ClientListObjectsRequest
            {
                User = $"user:{subject}",
                Relation = relation,
                Type = type
            },
            CreateListObjectsOptions(),
            cancellationToken: cancellationToken))
        {
            objects.Add(response.Object);
        }

        LogObjectsListed(_logger, subject, relation, type, objects.Count);
        return objects;
    }

    private async Task<OpenFgaClient> CreateStoreClientAsync(CancellationToken cancellationToken)
    {
        using var apiClient = new OpenFgaClient(new ClientConfiguration { ApiUrl = _apiUrl });
        var stores = await apiClient.ListStores(
            new ClientListStoresRequest { Name = _storeName },
            cancellationToken: cancellationToken);
        var store = stores.Stores.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, _storeName, StringComparison.Ordinal));

        if (store is null)
        {
            throw new InvalidOperationException($"OpenFGA store '{_storeName}' was not found.");
        }

        using var storeClient = new OpenFgaClient(new ClientConfiguration
        {
            ApiUrl = _apiUrl,
            StoreId = store.Id
        });
        var models = await storeClient.ReadAuthorizationModels(
            new ClientReadAuthorizationModelsOptions { PageSize = 1 },
            cancellationToken);
        var model = models.AuthorizationModels.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"OpenFGA store '{_storeName}' has no authorization model.");

        LogStoreResolved(_logger, _storeName, store.Id, model.Id);
        return new OpenFgaClient(new ClientConfiguration
        {
            ApiUrl = _apiUrl,
            StoreId = store.Id,
            AuthorizationModelId = model.Id
        });
    }

    public void Dispose() => _storeClient.Dispose();

    private static string GetSubject(ClaimsPrincipal user) =>
        user.FindFirstValue("sub")
        ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Authenticated users must have a subject claim.");

    internal static ClientCheckOptions CreateCheckOptions() =>
        new() { Consistency = ConsistencyPreference.HIGHERCONSISTENCY };

    internal static ClientListObjectsOptions CreateListObjectsOptions() =>
        new() { Consistency = ConsistencyPreference.HIGHERCONSISTENCY };

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Debug,
        Message = "OpenFGA checked {Relation} on {Object} for subject {Subject}; allowed: {Allowed}")]
    private static partial void LogPermissionChecked(
        ILogger logger,
        string subject,
        string relation,
        string @object,
        bool allowed);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Debug,
        Message = "Resolved OpenFGA store {StoreName} ({StoreId}) at authorization model {AuthorizationModelId}")]
    private static partial void LogStoreResolved(
        ILogger logger,
        string storeName,
        string storeId,
        string authorizationModelId);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Debug,
        Message = "OpenFGA wrote tuple {User}#{Relation}@{Object}")]
    private static partial void LogTupleWritten(
        ILogger logger,
        string user,
        string relation,
        string @object);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Debug,
        Message = "OpenFGA deleted tuple {User}#{Relation}@{Object}")]
    private static partial void LogTupleDeleted(
        ILogger logger,
        string user,
        string relation,
        string @object);

    [LoggerMessage(
        EventId = 3005,
        Level = LogLevel.Debug,
        Message = "OpenFGA listed {ObjectCount} {Type} objects for {Relation} and subject {Subject}")]
    private static partial void LogObjectsListed(
        ILogger logger,
        string subject,
        string relation,
        string type,
        int objectCount);

}

internal sealed class RetryableAsyncValue<T> : IDisposable
    where T : class
{
    private readonly Func<CancellationToken, Task<T>> _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private T? _value;
    private int _disposeStarted;

    public RetryableAsyncValue(Func<CancellationToken, Task<T>> factory) => _factory = factory;

    public async Task<T> GetValueAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        var current = Volatile.Read(ref _value);
        if (current is not null)
        {
            return current;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            current = Volatile.Read(ref _value);
            if (current is not null)
            {
                return current;
            }

            current = await _factory(cancellationToken);
            Volatile.Write(ref _value, current);
            return current;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        _gate.Wait();
        try
        {
            if (Interlocked.Exchange(ref _value, null) is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposeStarted) != 0,
            this);
    }
}

internal static class OpenFgaAuthorizationExtensions
{
    public static IServiceCollection AddOpenFgaAuthorization(
        this IServiceCollection services,
        bool enableRecoveryWorker = true)
    {
        services.AddSingleton<IOpenFgaAuthorization, OpenFgaAuthorization>();
        services.AddSingleton<OpenFgaTupleOutbox>();
        services.AddSingleton<IOpenFgaTupleOutbox>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenFgaTupleOutbox>());
        if (enableRecoveryWorker)
        {
            services.AddHostedService(serviceProvider =>
                serviceProvider.GetRequiredService<OpenFgaTupleOutbox>());
        }

        return services;
    }
}
