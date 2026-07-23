using NUnit.Framework;
using SymphonyTest1.Api.Infrastructure;

namespace SymphonyTest1.UnitTests.Infrastructure;

[TestFixture]
public class NpgsqlConnectionFactoryTests
{
    [Test]
    public void Constructor_WithValidConnectionString_DoesNotThrow()
    {
        // Arrange
        var connectionString = "Host=localhost;Database=test;Username=user;Password=pass";

        // Act & Assert
        Assert.DoesNotThrow(() => new NpgsqlConnectionFactory(connectionString));
    }

    [Test]
    public void Constructor_WithNullConnectionString_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new NpgsqlConnectionFactory(null!));
    }
}
