using AcceptanceTests.Environment;

#pragma warning disable CA1050 // This NUnit SetUpFixture intentionally applies assembly-wide.
[SetUpFixture]
public sealed class AcceptanceSetUp
{
    internal static AcceptanceOptions? Options { get; private set; }

    [OneTimeSetUp]
    public void RequireAnExternalSystem()
    {
        if (!AcceptanceOptions.TryLoad(out var options))
        {
            Assert.Ignore(
                "Acceptance tests require ACCEPTANCE_BASE_URL and run against a deployed system.");
        }

        Options = options;
    }
}
#pragma warning restore CA1050
