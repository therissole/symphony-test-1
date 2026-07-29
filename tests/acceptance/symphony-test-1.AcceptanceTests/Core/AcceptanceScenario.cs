using AcceptanceTests.TestData;

namespace AcceptanceTests.Core;

/// <summary>
/// Holds state that belongs to one acceptance scenario, including unique test data and public-boundary cleanup.
/// </summary>
internal sealed class AcceptanceScenario : IAsyncDisposable
{
    private readonly List<Func<CancellationToken, Task>> _cleanup = [];

    public AcceptanceScenario(ScenarioDataContext data) => Data = data;

    public ScenarioDataContext Data { get; }
    public string IsolationToken => Data.IsolationToken;
    public int Seed => Data.Seed;

    // Reverse order mirrors acquisition order, for example delete a child before its parent.
    public void TrackCleanup(Func<CancellationToken, Task> action) => _cleanup.Add(action);

    public async ValueTask DisposeAsync()
    {
        foreach (var action in _cleanup.AsEnumerable().Reverse())
        {
            try { await action(CancellationToken.None); }
            // Cleanup must not hide the scenario result; unique data provides the real isolation.
            catch { }
        }
    }
}
