using System.Text.RegularExpressions;

namespace SymphonyTest1.UnitTests.Architecture;

[TestFixture]
public sealed class TimeArchitectureTests
{
    private static readonly Regex[] ForbiddenApplicationTimeSources =
    [
        new(@"\bDateTime\.(Now|UtcNow|Today)\b", RegexOptions.CultureInvariant),
        new(@"\bDateTimeOffset\.(Now|UtcNow)\b", RegexOptions.CultureInvariant),
        new(@"\b(CURRENT_TIMESTAMP|CURRENT_DATE|CURRENT_TIME)\b", RegexOptions.IgnoreCase),
        new(@"\b(now|clock_timestamp|statement_timestamp|transaction_timestamp|localtimestamp|localtime)\s*\(", RegexOptions.IgnoreCase)
    ];

    [Test]
    public void ApplicationTime_ComesFromTimeProvider()
    {
        var solutionRoot = FindSolutionRoot();
        var sourceRoots = new[]
        {
            Path.Combine(solutionRoot, "src", "symphony-test-1.Api"),
            Path.Combine(solutionRoot, "src", "symphony-test-1.Web"),
            Path.Combine(solutionRoot, "src", "symphony-test-1.DatabaseMigrations")
        };

        var offenders = sourceRoots
            .SelectMany(root => Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
            .Where(path => Path.GetExtension(path) is ".cs" or ".razor")
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(ContainsForbiddenTimeSource)
            .Select(path => Path.GetRelativePath(solutionRoot, path))
            .ToList();

        Assert.That(offenders, Is.Empty,
            "Application behaviour must use an injected TimeProvider rather than a wall-clock API or database current-time function.");
    }

    [Test]
    public void NewMigrations_DoNotIntroduceDatabaseClockDefaults()
    {
        var solutionRoot = FindSolutionRoot();
        var migrationDirectory = Path.Combine(solutionRoot, "db", "migrations");
        var offenders = Directory.GetFiles(migrationDirectory, "V*.sql")
            .Where(path => !Path.GetFileName(path).StartsWith("V1__", StringComparison.Ordinal)
                && !Path.GetFileName(path).StartsWith("V2__", StringComparison.Ordinal))
            .Where(ContainsForbiddenTimeSource)
            .Select(Path.GetFileName)
            .ToList();

        Assert.That(offenders, Is.Empty,
            "V1 and V2 are checksum-protected history. Later migrations must not introduce database-owned current time.");
    }

    private static bool ContainsForbiddenTimeSource(string path)
    {
        var source = File.ReadAllText(path);
        return ForbiddenApplicationTimeSources.Any(pattern => pattern.IsMatch(source));
    }

    private static string FindSolutionRoot()
    {
        for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (directory.GetFiles("*.slnx").Length > 0)
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find the solution root.");
    }
}
