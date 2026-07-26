using AcceptanceTests.TestData;

namespace AcceptanceTests.Core;

internal sealed class AcceptanceScenario : IAsyncDisposable
{
    private readonly List<Func<CancellationToken, Task>> _cleanup = [];

    public AcceptanceScenario(ScenarioDataContext data) => Data = data;

    public ScenarioDataContext Data { get; }
    public string IsolationToken => Data.IsolationToken;
    public int Seed => Data.Seed;

    public void TrackCleanup(Func<CancellationToken, Task> action) => _cleanup.Add(action);

    public async ValueTask DisposeAsync()
    {
        foreach (var action in _cleanup.AsEnumerable().Reverse())
        {
            try { await action(CancellationToken.None); }
            catch { }
        }
    }
}
