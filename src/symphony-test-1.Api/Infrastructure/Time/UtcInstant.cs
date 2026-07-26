namespace SymphonyTest1.Api.Infrastructure.Time;

public static class UtcInstant
{
    public static DateTimeOffset FromDatabase(DateTime value) =>
        value.Kind == DateTimeKind.Local
            ? new DateTimeOffset(value.ToUniversalTime())
            : new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
