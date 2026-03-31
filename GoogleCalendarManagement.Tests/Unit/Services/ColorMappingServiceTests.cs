using FluentAssertions;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class ColorMappingServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("unknown")]
    public void GetHexColor_AlwaysReturnsAzureFallback(string? colorId)
    {
        var service = new ColorMappingService();

        var result = service.GetHexColor(colorId);

        result.Should().Be("#0088CC");
    }
}
