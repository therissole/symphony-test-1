using System.Security.Claims;

using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Configuration;

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
}

internal sealed partial class OpenFgaAuthorization : IOpenFgaAuthorization
{
    private readonly string _apiUrl;
    private readonly string _storeName;
    private readonly ILogger<OpenFgaAuthorization> _logger;
    private readonly Lazy<Task<OpenFgaClient>> _storeClient;

    public OpenFgaAuthorization(
        IConfiguration configuration,
        ILogger<OpenFgaAuthorization> logger)
    {
        _apiUrl = configuration["OpenFga:ApiUrl"]
            ?? throw new InvalidOperationException("OpenFga:ApiUrl is required.");
        _storeName = configuration["OpenFga:StoreName"]
            ?? throw new InvalidOperationException("OpenFga:StoreName is required.");
        _logger = logger;
        _storeClient = new Lazy<Task<OpenFgaClient>>(CreateStoreClientAsync);
    }

    public async Task<bool> IsAllowedAsync(
        ClaimsPrincipal user,
        string relation,
        string @object,
        CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue("sub")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated users must have a subject claim.");

        var client = await _storeClient.Value;
        var check = await client.Check(
            new ClientCheckRequest
            {
                User = $"user:{subject}",
                Relation = relation,
                Object = @object
            },
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
        var client = await _storeClient.Value;
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
            cancellationToken: cancellationToken);

        LogTupleWritten(_logger, user, relation, @object);
    }

    public async Task DeleteTupleAsync(
        string user,
        string relation,
        string @object,
        CancellationToken cancellationToken)
    {
        var client = await _storeClient.Value;
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
            cancellationToken: cancellationToken);

        LogTupleDeleted(_logger, user, relation, @object);
    }

    private async Task<OpenFgaClient> CreateStoreClientAsync()
    {
        var apiClient = new OpenFgaClient(new ClientConfiguration { ApiUrl = _apiUrl });
        var stores = await apiClient.ListStores(new ClientListStoresRequest { Name = _storeName });
        var store = stores.Stores.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, _storeName, StringComparison.Ordinal));

        if (store is null)
        {
            throw new InvalidOperationException($"OpenFGA store '{_storeName}' was not found.");
        }

        LogStoreResolved(_logger, _storeName, store.Id);
        return new OpenFgaClient(new ClientConfiguration
        {
            ApiUrl = _apiUrl,
            StoreId = store.Id
        });
    }

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
        Message = "Resolved OpenFGA store {StoreName} ({StoreId})")]
    private static partial void LogStoreResolved(
        ILogger logger,
        string storeName,
        string storeId);

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

}

internal static class OpenFgaAuthorizationExtensions
{
    public static IServiceCollection AddOpenFgaAuthorization(
        this IServiceCollection services) =>
        services.AddSingleton<IOpenFgaAuthorization, OpenFgaAuthorization>();
}
