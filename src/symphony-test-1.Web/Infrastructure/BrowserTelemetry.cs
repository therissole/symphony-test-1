using System.Diagnostics;

namespace SymphonyTest1.Web.Infrastructure;

internal static class BrowserTelemetry
{
    /// <summary>
    /// Replaces Blazor's implementation-oriented event span name with the user's action.
    /// </summary>
    public static void NameCurrentAction(string displayName)
    {
        if (Activity.Current is { } activity)
        {
            activity.DisplayName = displayName;
        }
    }
}
