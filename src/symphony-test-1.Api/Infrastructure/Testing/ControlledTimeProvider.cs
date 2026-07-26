namespace SymphonyTest1.Api.Infrastructure.Testing;

public sealed class ControlledTimeProvider : TimeProvider
{
    private readonly TimeProvider _fallback;
    private readonly Lock _lock = new();
    private DateTimeOffset? _utcNow;

    public ControlledTimeProvider(TimeProvider fallback)
    {
        _fallback = fallback;
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _utcNow ?? _fallback.GetUtcNow();
        }
    }

    public void SetUtcNow(DateTimeOffset utcNow)
    {
        lock (_lock)
        {
            _utcNow = utcNow.ToUniversalTime();
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _utcNow = null;
        }
    }
}
