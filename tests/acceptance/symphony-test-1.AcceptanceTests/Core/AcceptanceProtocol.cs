namespace AcceptanceTests.Core;

public enum AcceptanceProtocol
{
    Api,
    Web
}

[AttributeUsage(AttributeTargets.Class)]
internal sealed class AcceptanceProtocolsAttribute(params AcceptanceProtocol[] protocols) : Attribute
{
    public IReadOnlyList<AcceptanceProtocol> Protocols { get; } = protocols;
}
