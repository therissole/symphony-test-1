using System.Reflection;

namespace AcceptanceTests.Core;

internal static class ProtocolTestCaseSource
{
    public static AcceptanceProtocol Current =>
        TestContext.CurrentContext.Test.Arguments.SingleOrDefault() is AcceptanceProtocol protocol
            ? protocol
            : throw new InvalidOperationException("Acceptance protocol was not supplied to the test case.");

    public static IEnumerable<TestCaseData> For(Type fixtureType)
    {
        var selection = fixtureType.GetCustomAttribute<AcceptanceProtocolsAttribute>()?.Protocols;
        var protocols = selection is null || selection.Count == 0
            ? new[] { AcceptanceProtocol.Api, AcceptanceProtocol.Web }
            : selection;

        return protocols.Select(protocol => new TestCaseData(protocol)
        {
            TestName = $"{fixtureType.Name} via {protocol}"
        });
    }
}
