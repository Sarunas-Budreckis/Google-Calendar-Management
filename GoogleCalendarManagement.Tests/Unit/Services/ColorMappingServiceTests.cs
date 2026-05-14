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
        _sut.GetHexColor(colorId).Should().Be("#00AAFF");
    }

    [Theory]
    [InlineData("azure",    "#00AAFF")]
    [InlineData("red",      "#DA5234")]
    [InlineData("flamingo", "#D6837A")]
    [InlineData("orange",   "#E3683E")]
    [InlineData("banana",   "#E7BA51")]
    [InlineData("sage",     "#55B080")]
    [InlineData("basil",    "#489160")]
    [InlineData("peacock",  "#4B99D2")]
    [InlineData("navy",     "#6E72C3")]
    [InlineData("lavender", "#828BC2")]
    [InlineData("grape",    "#A75ABA")]
    [InlineData("graphite", "#7C7C7C")]
    public void GetHexColor_CanonicalKey_ReturnsMappedDisplayHex(string colorId, string expectedHex)
    {
        _sut.GetHexColor(colorId).Should().Be(expectedHex);
    }

    [Theory]
    [InlineData("11", "red")]
    [InlineData("4", "flamingo")]
    [InlineData("6", "orange")]
    [InlineData("5", "banana")]
    [InlineData("2", "sage")]
    [InlineData("10", "basil")]
    [InlineData("7", "peacock")]
    [InlineData("9", "navy")]
    [InlineData("1", "lavender")]
    [InlineData("3", "grape")]
    [InlineData("8", "graphite")]
    [InlineData("0", "azure")]
    public void NormalizeColorKey_GoogleAlias_ReturnsCanonicalKey(string rawColorId, string expectedKey)
    {
        _sut.NormalizeColorKey(rawColorId).Should().Be(expectedKey);
    }

    [Theory]
    [InlineData("tomato", "red")]
    [InlineData("tangerine", "orange")]
    [InlineData("blueberry", "navy")]
    [InlineData("purple", "navy")]
    [InlineData("grey", "graphite")]
    [InlineData("gray", "graphite")]
    [InlineData("yellow", "banana")]
    [InlineData("navy", "navy")]
    public void NormalizeColorKey_LegacyLocalAlias_ReturnsGoogleCanonicalKey(string rawColorId, string expectedKey)
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
    [InlineData("11", "Red")]
    [InlineData("red", "Red")]
    [InlineData("tomato", "Red")]
    [InlineData("4", "Flamingo")]
    [InlineData("6", "Orange")]
    [InlineData("5", "Banana")]
    [InlineData("2", "Sage")]
    [InlineData("10", "Basil")]
    [InlineData("7", "Peacock")]
    [InlineData("9", "Navy")]
    [InlineData("1", "Lavender")]
    [InlineData("3", "Grape")]
    [InlineData("8", "Graphite")]
    [InlineData("0", "Azure")]
    [InlineData("azure", "Azure")]
    public void GetDisplayName_KnownId_ReturnsMappedName(string colorId, string expectedName)
    {
        _sut.GetDisplayName(colorId).Should().Be(expectedName);
    }

    [Fact]
    public void PickerColors_ContainsExactlyTwelveOrderedOptions()
    {
        _sut.PickerColors.Select(option => option.Key).Should().Equal(
            "red",
            "flamingo",
            "orange",
            "banana",
            "sage",
            "basil",
            "peacock",
            "navy",
            "lavender",
            "grape",
            "graphite",
            "azure");
        _sut.PickerColors.Should().OnlyContain(option => option.ContrastTextHex == "#FFFFFF");
    }

    [Fact]
    public void AllColors_ContainsCanonicalAndNumericAliases()
    {
        _sut.AllColors.Should().ContainKeys(
            "red",
            "tomato",
            "flamingo",
            "orange",
            "tangerine",
            "banana",
            "sage",
            "basil",
            "peacock",
            "navy",
            "blueberry",
            "lavender",
            "grape",
            "graphite",
            "azure",
            "11",
            "4",
            "6",
            "5",
            "2",
            "10",
            "7",
            "9",
            "1",
            "3",
            "8",
            "0");
        foreach (var hex in _sut.AllColors.Values)
        {
            hex.Should().MatchRegex("^#[0-9A-Fa-f]{6}$");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("azure")]
    [InlineData("unknown")]
    public void GetGoogleColorId_AzureOrFallback_ReturnsNull(string? colorId)
    {
        _sut.GetGoogleColorId(colorId).Should().BeNull();
    }

    [Theory]
    [InlineData("red", "11")]
    [InlineData("tomato", "11")]
    [InlineData("flamingo", "4")]
    [InlineData("orange", "6")]
    [InlineData("tangerine", "6")]
    [InlineData("banana", "5")]
    [InlineData("sage", "2")]
    [InlineData("basil", "10")]
    [InlineData("peacock", "7")]
    [InlineData("navy", "9")]
    [InlineData("blueberry", "9")]
    [InlineData("lavender", "1")]
    [InlineData("grape", "3")]
    [InlineData("graphite", "8")]
    public void GetGoogleColorId_NonAzureCanonicalKey_ReturnsMappedId(string colorId, string expectedGoogleId)
    {
        _sut.GetGoogleColorId(colorId).Should().Be(expectedGoogleId);
    }
}
