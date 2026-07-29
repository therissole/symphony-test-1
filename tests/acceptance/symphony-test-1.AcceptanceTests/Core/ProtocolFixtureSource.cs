using System.Reflection;

namespace AcceptanceTests.Core;

/// <summary>
/// Expands a feature's declared protocol coverage into NUnit test cases without duplicating its BDD scenario.
/// </summary>
internal static class ProtocolTestCaseSource
{
    // NUnit supplies the protocol as the scenario argument; setup reads it before the scenario body runs.
    public static AcceptanceProtocol Current =>
        TestContext.CurrentContext.Test.Arguments.SingleOrDefault() is AcceptanceProtocol protocol
            ? protocol
            : throw new InvalidOperationException("Acceptance protocol was not supplied to the test case.");

    public static IEnumerable<TestCaseData> For(Type fixtureType)
    {
        var selection = fixtureType.GetCustomAttribute<AcceptanceProtocolsAttribute>()?.Protocols;
        // A scenario proves both public channels unless it declares a channel-specific exception.
        var protocols = selection is null || selection.Count == 0
            ? new[] { AcceptanceProtocol.Api, AcceptanceProtocol.Web }
            : selection;

        return protocols.Select(protocol => new TestCaseData(protocol)
        {
            TestName = $"{fixtureType.Name} via {protocol}"
        });
    }
}
