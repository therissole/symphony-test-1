using System.Text.Json;

using SymphonyTest1.Api.Infrastructure.Identifiers;

namespace SymphonyTest1.UnitTests.Infrastructure;

[TestFixture]
public sealed class EntityIdTests
{
    private static readonly Guid Value = Guid.Parse("4f260bc6-7947-407d-841e-ecfc4869885e");

    [Test]
    public void LanguageId_PreservesTheUuidWireContract()
    {
        AssertWireContract(
            new LanguageId(Value),
            static value => value.Value,
            static text => LanguageId.Parse(text, provider: null),
            static (string text, out LanguageId result) =>
                LanguageId.TryParse(text, provider: null, out result));
    }

    [Test]
    public void GreetingId_PreservesTheUuidWireContract()
    {
        AssertWireContract(
            new GreetingId(Value),
            static value => value.Value,
            static text => GreetingId.Parse(text, provider: null),
            static (string text, out GreetingId result) =>
                GreetingId.TryParse(text, provider: null, out result));
    }

    private static void AssertWireContract<TId>(
        TId identifier,
        Func<TId, Guid> getValue,
        Func<string, TId> parse,
        TryParse<TId> tryParse)
    {
        var json = JsonSerializer.Serialize(identifier);
        var roundTrip = JsonSerializer.Deserialize<TId>(json);

        Assert.Multiple(() =>
        {
            Assert.That(json, Is.EqualTo($"\"{Value}\""));
            Assert.That(getValue(roundTrip!), Is.EqualTo(Value));
            Assert.That(getValue(parse(Value.ToString())), Is.EqualTo(Value));
            Assert.That(tryParse(Value.ToString(), out var parsed), Is.True);
            Assert.That(getValue(parsed), Is.EqualTo(Value));
            Assert.That(tryParse("not-a-uuid", out _), Is.False);
        });
    }

    private delegate bool TryParse<T>(string text, out T result);
}
