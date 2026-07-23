using FluentValidation;
using SymphonyTest1.Api.Features.Languages;

namespace SymphonyTest1.UnitTests.Features.Languages;

[TestFixture]
public class LanguageValidationTests
{
    [TestCase("", "en", "name")]
    [TestCase("English", "", "code")]
    [TestCase("English", "code-that-is-too-long", "code")]
    public void CreateLanguage_WithInvalidInput_ReturnsExpectedValidationError(
        string name,
        string code,
        string expectedKey)
    {
        var validator = new CreateLanguage.RequestValidator();
        var result = validator.Validate(new CreateLanguage.Request(name, code));

        Assert.That(result.ToDictionary(), Does.ContainKey(expectedKey));
    }

    [Test]
    public void UpdateLanguage_WithValidInput_ReturnsNoValidationErrors()
    {
        var validator = new UpdateLanguage.RequestValidator();
        var result = validator.Validate(new UpdateLanguage.Request("English", "en"));

        Assert.That(result.IsValid, Is.True);
    }
}
