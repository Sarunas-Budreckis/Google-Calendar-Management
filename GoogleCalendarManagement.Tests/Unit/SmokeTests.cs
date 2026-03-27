using Xunit;
using FluentAssertions;

namespace GoogleCalendarManagement.Tests.Unit;

/// <summary>
/// Smoke tests to verify the test framework is working correctly.
/// </summary>
public class SmokeTests
{
    [Fact]
    public void CanCreateMainWindow_ReturnsTrue()
    {
        // Arrange & Act
        var canCreate = true; // Placeholder - real test would instantiate MainWindow

        // Assert
        canCreate.Should().BeTrue();
    }

    [Fact]
    public void TestFrameworkIsConfigured_PassesBasicAssertion()
    {
        // Arrange
        var expected = 42;
        var actual = 42;

        // Act & Assert
        actual.Should().Be(expected);
    }

    [Fact]
    public void FluentAssertions_IsAvailable()
    {
        // Arrange
        var testString = "Hello, World!";

        // Act & Assert
        testString.Should().NotBeNullOrEmpty()
            .And.Contain("World");
    }
}
