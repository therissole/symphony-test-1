namespace AcceptanceTests.Core;

/// <summary>The public boundary through which an acceptance scenario exercises the deployed system.</summary>
public enum AcceptanceProtocol
{
    Api,
    Web
}

// Omit this attribute to run a feature through every supported public boundary.
[AttributeUsage(AttributeTargets.Class)]
internal sealed class AcceptanceProtocolsAttribute(params AcceptanceProtocol[] protocols) : Attribute
{
    public IReadOnlyList<AcceptanceProtocol> Protocols { get; } = protocols;
}
