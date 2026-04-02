using FluentAssertions;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class ColorMappingServiceTests
{
    private readonly ColorMappingService _sut = new();

    // ── Fallback ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("99")]
    public void GetHexColor_UnknownOrEmpty_ReturnsFallbackAzure(string? colorId)
    {
        _sut.GetHexColor(colorId).Should().Be("#0088CC");
    }

    // ── Alias lookups (case-insensitive) ──────────────────────────────────────

    [Theory]
    [InlineData("lavender", "#7986CB")]
    [InlineData("LAVENDER", "#7986CB")]
    [InlineData("Lavender", "#7986CB")]
    [InlineData("azure",    "#0088CC")]
    [InlineData("AZURE",    "#0088CC")]
    [InlineData("purple",   "#3F51B5")]
    [InlineData("PURPLE",   "#3F51B5")]
    [InlineData("grey",     "#616161")]
    [InlineData("GREY",     "#616161")]
    [InlineData("yellow",   "#F6BF26")]
    [InlineData("navy",     "#33B679")]
    [InlineData("sage",     "#0B8043")]
    [InlineData("flamingo", "#E67C73")]
    [InlineData("orange",   "#F4511E")]
    public void GetHexColor_AliasInput_ReturnsMappedHex(string alias, string expectedHex)
    {
        _sut.GetHexColor(alias).Should().Be(expectedHex);
    }

    // ── Numeric ID lookups ────────────────────────────────────────────────────

    [Theory]
    [InlineData("1",  "#7986CB")]   // lavender  (GCal event colorId 1 = Lavender)
    [InlineData("9",  "#3F51B5")]   // purple
    [InlineData("8",  "#616161")]   // grey
    [InlineData("5",  "#F6BF26")]   // yellow
    [InlineData("2",  "#33B679")]   // navy
    [InlineData("10", "#0B8043")]   // sage
    [InlineData("4",  "#E67C73")]   // flamingo
    [InlineData("6",  "#F4511E")]   // orange
    [InlineData("3",  "#8E24AA")]   // grape
    public void GetHexColor_NumericIdInput_ReturnsMappedHex(string id, string expectedHex)
    {
        _sut.GetHexColor(id).Should().Be(expectedHex);
    }

    // ── AllColors ─────────────────────────────────────────────────────────────

    [Fact]
    public void AllColors_Contains18Entries_AliasAndNumericForEach()
    {
        // 9 categories × 2 keys each (alias + numeric ID)
        _sut.AllColors.Should().HaveCount(18);
    }

    [Fact]
    public void AllColors_ValuesAreValidSixDigitHex()
    {
        foreach (var hex in _sut.AllColors.Values)
        {
            hex.Should().MatchRegex(@"^#[0-9A-Fa-f]{6}$",
                because: $"'{hex}' must be a valid #RRGGBB hex colour");
        }
    }
}
