using OpenFga.Sdk.Model;
using SymphonyTest1.Api.Infrastructure.Authorization;

namespace SymphonyTest1.UnitTests.Infrastructure.Authorization;

[TestFixture]
public sealed class OpenFgaAuthorizationPolicyTests
{
    [Test]
    public void CheckRequests_PreferHigherConsistency()
    {
        var options = OpenFgaAuthorization.CreateCheckOptions();

        Assert.That(options.Consistency, Is.EqualTo(ConsistencyPreference.HIGHERCONSISTENCY));
    }

    [Test]
    public void ListObjectsRequests_PreferHigherConsistency()
    {
        var options = OpenFgaAuthorization.CreateListObjectsOptions();

        Assert.That(options.Consistency, Is.EqualTo(ConsistencyPreference.HIGHERCONSISTENCY));
    }

    [Test]
    public async Task StoreClientInitialization_RetriesAfterATransientFailureThenCachesSuccess()
    {
        var expected = new object();
        var attempts = 0;
        using var value = new RetryableAsyncValue<object>(_ =>
        {
            attempts++;
            return attempts == 1
                ? Task.FromException<object>(new InvalidOperationException("Transient failure."))
                : Task.FromResult(expected);
        });

        Assert.That(
            async () => await value.GetValueAsync(CancellationToken.None),
            Throws.TypeOf<InvalidOperationException>());

        var recovered = await value.GetValueAsync(CancellationToken.None);
        var cached = await value.GetValueAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(recovered, Is.SameAs(expected));
            Assert.That(cached, Is.SameAs(expected));
            Assert.That(attempts, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task StoreClientInitialization_DisposesTheCachedClient()
    {
        var client = new DisposableClient();
        var value = new RetryableAsyncValue<DisposableClient>(_ => Task.FromResult(client));

        Assert.That(
            await value.GetValueAsync(CancellationToken.None),
            Is.SameAs(client));

        value.Dispose();
        value.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(client.IsDisposed, Is.True);
            Assert.That(
                async () => await value.GetValueAsync(CancellationToken.None),
                Throws.TypeOf<ObjectDisposedException>());
        });
    }

    private sealed class DisposableClient : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
