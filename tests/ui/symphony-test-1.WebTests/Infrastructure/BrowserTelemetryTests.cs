using System.Diagnostics;
using SymphonyTest1.Web.Infrastructure;

namespace SymphonyTest1.WebTests.Infrastructure;

[TestFixture]
public sealed class BrowserTelemetryTests
{
    [Test]
    public void NameCurrentAction_ReplacesTheActiveActivityDisplayName()
    {
        using var activity = new Activity("framework-generated event");
        activity.Start();

        BrowserTelemetry.NameCurrentAction("Create language clicked");

        Assert.That(activity.DisplayName, Is.EqualTo("Create language clicked"));
    }
}
