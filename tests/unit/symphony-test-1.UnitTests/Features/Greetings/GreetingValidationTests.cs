using FluentValidation;
using SymphonyTest1.Api.Features.Greetings;

namespace SymphonyTest1.UnitTests.Features.Greetings;

[TestFixture]
public class GreetingValidationTests
{
    [Test]
    public void CreateGreeting_WithEmptyLanguageId_ReturnsValidationError()
    {
        var validator = new CreateGreeting.RequestValidator();
        var result = validator.Validate(new CreateGreeting.Request(Guid.Empty, "Hello", false));

        Assert.That(result.ToDictionary(), Does.ContainKey("languageId"));
    }

    [Test]
    public void UpdateGreeting_WithEmptyText_ReturnsValidationError()
    {
        var validator = new UpdateGreeting.RequestValidator();
        var result = validator.Validate(new UpdateGreeting.Request(Guid.NewGuid(), "", false));

        Assert.That(result.ToDictionary(), Does.ContainKey("greetingText"));
    }

    [Test]
    public void ListGreetings_WithAnInvalidTimeRange_ReturnsValidationError()
    {
        var validator = new ListGreetings.RequestValidator();
        var result = validator.Validate(new ListGreetings.Request(
            null,
            null,
            new DateTimeOffset(2026, 7, 27, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 26, 9, 0, 0, TimeSpan.Zero)));

        Assert.That(result.ToDictionary(), Does.ContainKey("createdTo"));
    }
}
