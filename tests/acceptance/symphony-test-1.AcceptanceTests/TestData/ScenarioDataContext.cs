using System.Security.Cryptography;
using System.Text;

namespace AcceptanceTests.TestData;

internal sealed class ScenarioDataContext
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public ScenarioDataContext()
    {
        IsolationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        Seed = BitConverter.ToInt32(RandomNumberGenerator.GetBytes(sizeof(int)));
    }

    public string IsolationToken { get; }
    public int Seed { get; }

    public string LanguageName(string alias) => Resolve(
        $"language-name:{alias}",
        // Logical aliases keep scenarios readable while physical values avoid cross-run collisions.
        () => $"{alias} [{IsolationToken}]");

    public string LanguageCode(string alias) => Resolve(
        $"language-code:{alias}",
        () => $"{Slug(alias)[..Math.Min(2, Slug(alias).Length)]}{IsolationToken}"[..10]);

    private string Resolve(string key, Func<string> create) =>
        _values.TryGetValue(key, out var value)
            ? value
            : _values[key] = create();

    private static string Slug(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.Length == 0 ? "lg" : builder.ToString();
    }
}
