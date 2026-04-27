using FluentAssertions;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class ColorMappingServiceTests
{
    private readonly ColorMappingService _sut = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("99")]
    public void GetHexColor_UnknownOrEmpty_ReturnsFallbackAzure(string? colorId)
    {
        _sut.GetHexColor(colorId).Should().Be("#0088CC");
    }

    [Theory]
    [InlineData("azure", "#0088CC")]
    [InlineData("purple", "#3F51B5")]
    [InlineData("grey", "#616161")]
    [InlineData("yellow", "#F6BF26")]
    [InlineData("navy", "#33B679")]
    [InlineData("sage", "#0B8043")]
    [InlineData("flamingo", "#E67C73")]
    [InlineData("orange", "#F4511E")]
    [InlineData("lavender", "#8E24AA")]
    public void GetHexColor_CanonicalKey_ReturnsMappedHex(string colorId, string expectedHex)
    {
        _sut.GetHexColor(colorId).Should().Be(expectedHex);
    }

    [Theory]
    [InlineData("1", "azure")]
    [InlineData("9", "purple")]
    [InlineData("8", "grey")]
    [InlineData("5", "yellow")]
    [InlineData("2", "navy")]
    [InlineData("10", "sage")]
    [InlineData("4", "flamingo")]
    [InlineData("6", "orange")]
    [InlineData("3", "lavender")]
    public void NormalizeColorKey_GoogleAlias_ReturnsCanonicalKey(string rawColorId, string expectedKey)
    {
        _sut.NormalizeColorKey(rawColorId).Should().Be(expectedKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("99")]
    public void GetDisplayName_UnknownOrEmpty_ReturnsFallbackAzure(string? colorId)
    {
        _sut.GetDisplayName(colorId).Should().Be("Azure");
    }

    [Theory]
    [InlineData("1", "Azure")]
    [InlineData("azure", "Azure")]
    [InlineData("9", "Purple")]
    [InlineData("8", "Grey")]
    [InlineData("5", "Yellow")]
    [InlineData("2", "Navy")]
    [InlineData("10", "Sage")]
    [InlineData("4", "Flamingo")]
    [InlineData("6", "Orange")]
    [InlineData("3", "Lavender")]
    public void GetDisplayName_KnownId_ReturnsMappedName(string colorId, string expectedName)
    {
        _sut.GetDisplayName(colorId).Should().Be(expectedName);
    }

    [Fact]
    public void PickerColors_ContainsExactlyNineOrderedOptions()
    {
        _sut.PickerColors.Select(option => option.Key).Should().Equal(
            "azure",
            "purple",
            "grey",
            "yellow",
            "navy",
            "sage",
            "flamingo",
            "orange",
            "lavender");
        _sut.PickerColors.Should().OnlyContain(option => option.ContrastTextHex == "#FFFFFF");
    }

    [Fact]
    public void AllColors_ContainsCanonicalAndNumericAliases()
    {
        _sut.AllColors.Should().HaveCount(18);
        foreach (var hex in _sut.AllColors.Values)
        {
            hex.Should().MatchRegex("^#[0-9A-Fa-f]{6}$");
        }
    }
}
