using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;

namespace SymphonyTest1.OpenFgaProvisioning;

internal static class BootstrapRoleReconciler
{
    private const string SystemObject = "system:global";
    private const string SuperuserRelation = "superuser";
    private const string StandardUserRelation = "standard_user";
    private const int WriteBatchSize = 100;

    public static async Task<BootstrapRoleReconciliationResult> ReconcileAsync(
        OpenFgaClient client,
        IEnumerable<string> superuserSubjects,
        IEnumerable<string> standardUserSubjects,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var desired = CreateDesiredTuples(superuserSubjects, standardUserSubjects);
        var current = await ReadCurrentTuplesAsync(client, cancellationToken);
        var plan = CreatePlan(current, desired);

        foreach (var batch in plan.Deletes.Chunk(WriteBatchSize))
        {
            await client.Write(
                new ClientWriteRequest
                {
                    Deletes = batch.Select(tuple => new ClientTupleKeyWithoutCondition
                    {
                        User = tuple.User,
                        Relation = tuple.Relation,
                        Object = tuple.Object
                    }).ToList()
                },
                new ClientWriteOptions
                {
                    Conflict = new ConflictOptions
                    {
                        OnMissingDeletes = OnMissingDeletes.Ignore
                    }
                },
                cancellationToken);
        }

        foreach (var batch in plan.Writes.Chunk(WriteBatchSize))
        {
            await client.Write(
                new ClientWriteRequest
                {
                    Writes = batch.Select(tuple => new ClientTupleKey
                    {
                        User = tuple.User,
                        Relation = tuple.Relation,
                        Object = tuple.Object
                    }).ToList()
                },
                new ClientWriteOptions
                {
                    Conflict = new ConflictOptions
                    {
                        OnDuplicateWrites = OnDuplicateWrites.Ignore
                    }
                },
                cancellationToken);
        }

        return new BootstrapRoleReconciliationResult(
            desired.Count,
            plan.Writes.Count,
            plan.Deletes.Count);
    }

    internal static BootstrapRoleReconciliationPlan CreatePlan(
        IEnumerable<BootstrapRoleTuple> current,
        IReadOnlySet<BootstrapRoleTuple> desired)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(desired);

        var managedCurrent = current
            .Where(IsManagedTuple)
            .ToHashSet();

        return new BootstrapRoleReconciliationPlan(
            desired.Except(managedCurrent).OrderBy(TupleSortKey, StringComparer.Ordinal).ToArray(),
            managedCurrent.Except(desired).OrderBy(TupleSortKey, StringComparer.Ordinal).ToArray());
    }

    internal static IReadOnlySet<BootstrapRoleTuple> CreateDesiredTuples(
        IEnumerable<string> superuserSubjects,
        IEnumerable<string> standardUserSubjects)
    {
        ArgumentNullException.ThrowIfNull(superuserSubjects);
        ArgumentNullException.ThrowIfNull(standardUserSubjects);

        return superuserSubjects
            .Select(subject => CreateTuple(subject, SuperuserRelation))
            .Concat(standardUserSubjects.Select(subject => CreateTuple(subject, StandardUserRelation)))
            .ToHashSet();
    }

    private static async Task<HashSet<BootstrapRoleTuple>> ReadCurrentTuplesAsync(
        OpenFgaClient client,
        CancellationToken cancellationToken)
    {
        var tuples = new HashSet<BootstrapRoleTuple>();

        await ReadRelationTuplesAsync(client, SuperuserRelation, tuples, cancellationToken);
        await ReadRelationTuplesAsync(client, StandardUserRelation, tuples, cancellationToken);

        return tuples;
    }

    private static async Task ReadRelationTuplesAsync(
        OpenFgaClient client,
        string relation,
        HashSet<BootstrapRoleTuple> tuples,
        CancellationToken cancellationToken)
    {
        string? continuationToken = null;

        do
        {
            var response = await client.Read(
                new ClientReadRequest
                {
                    Relation = relation,
                    Object = SystemObject
                },
                new ClientReadOptions
                {
                    PageSize = WriteBatchSize,
                    ContinuationToken = continuationToken
                },
                cancellationToken);

            foreach (var tuple in response.Tuples)
            {
                tuples.Add(new BootstrapRoleTuple(
                    tuple.Key.User,
                    tuple.Key.Relation,
                    tuple.Key.Object));
            }

            continuationToken = response.ContinuationToken;
        }
        while (!string.IsNullOrEmpty(continuationToken));
    }

    private static BootstrapRoleTuple CreateTuple(string subject, string relation) =>
        new($"user:{subject}", relation, SystemObject);

    private static bool IsManagedTuple(BootstrapRoleTuple tuple) =>
        string.Equals(tuple.Object, SystemObject, StringComparison.Ordinal)
        && (string.Equals(tuple.Relation, SuperuserRelation, StringComparison.Ordinal)
            || string.Equals(tuple.Relation, StandardUserRelation, StringComparison.Ordinal));

    private static string TupleSortKey(BootstrapRoleTuple tuple) =>
        $"{tuple.Object}\u001f{tuple.Relation}\u001f{tuple.User}";
}

internal readonly record struct BootstrapRoleTuple(
    string User,
    string Relation,
    string Object);

internal sealed record BootstrapRoleReconciliationPlan(
    IReadOnlyList<BootstrapRoleTuple> Writes,
    IReadOnlyList<BootstrapRoleTuple> Deletes);

internal readonly record struct BootstrapRoleReconciliationResult(
    int DesiredCount,
    int AddedCount,
    int RemovedCount);
