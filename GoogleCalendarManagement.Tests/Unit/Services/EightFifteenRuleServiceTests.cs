using FluentAssertions;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class EightFifteenRuleServiceTests
{
    private readonly EightFifteenRuleService _sut = new();

    private static DateTime Utc(int hour, int minute = 0) =>
        new(2026, 06, 04, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void ApplyRule_WhenTripUnder8Min_ReturnsOneBlock()
    {
        var start = Utc(8, 0);
        var end = Utc(8, 4);

        var blocks = _sut.ApplyRule(start, end);

        blocks.Should().HaveCount(1);
    }

    [Fact]
    public void ApplyRule_WhenTripExactly8Min_ReturnsOneBlock()
    {
        var start = Utc(8, 0);
        var end = Utc(8, 8);

        var blocks = _sut.ApplyRule(start, end);

        blocks.Should().HaveCount(1);
    }

    [Fact]
    public void ApplyRule_WhenTripExactly15Min_ReturnsOneBlock()
    {
        var start = Utc(8, 0);
        var end = Utc(8, 15);

        var blocks = _sut.ApplyRule(start, end);

        blocks.Should().HaveCount(1);
    }

    [Fact]
    public void ApplyRule_WhenTripIs20Min_ReturnsTwoBlocks()
    {
        var start = Utc(8, 0);
        var end = Utc(8, 20);

        var blocks = _sut.ApplyRule(start, end);

        // block 0: 15 min (>= 8, keep); block 1: 5 min (last, keep)
        blocks.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyRule_WhenTripIs30Min_ReturnsTwoBlocks()
    {
        var start = Utc(8, 0);
        var end = Utc(8, 30);

        var blocks = _sut.ApplyRule(start, end);

        blocks.Should().HaveCount(2);
    }

    [Fact]
    public void ApplyRule_WhenTripIs35Min_ReturnsThreeBlocks()
    {
        var start = Utc(8, 0);
        var end = Utc(8, 35);

        var blocks = _sut.ApplyRule(start, end);

        // block 0: 15 min (keep), block 1: 15 min (keep), block 2: 5 min (last, keep)
        blocks.Should().HaveCount(3);
    }

    [Fact]
    public void ApplyRule_WhenTripIs10Min_BlockTimesAreRoundedToQuarterHour()
    {
        var start = Utc(8, 7);
        var end = Utc(8, 17);

        var blocks = _sut.ApplyRule(start, end);

        blocks.Should().HaveCount(1);
        // 8:07 rounds to 8:00; 8:17 rounds to 8:15
        blocks[0].Start.Should().Be(new DateTime(2026, 06, 04, 8, 0, 0, DateTimeKind.Utc));
        blocks[0].End.Should().Be(new DateTime(2026, 06, 04, 8, 15, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ApplyRule_WhenEndEqualsStart_ReturnsSingleMinBlock()
    {
        var start = Utc(8, 0);

        var blocks = _sut.ApplyRule(start, start);

        blocks.Should().HaveCount(1);
        blocks[0].End.Should().Be(blocks[0].Start.AddMinutes(15));
    }

    [Fact]
    public void ApplyRule_WhenEndBeforeStart_ReturnsSingleMinBlock()
    {
        var start = Utc(8, 30);
        var end = Utc(8, 0);

        var blocks = _sut.ApplyRule(start, end);

        blocks.Should().HaveCount(1);
        blocks[0].End.Should().Be(blocks[0].Start.AddMinutes(15));
    }

    [Fact]
    public void ApplyRule_BlockEndIsNeverBeforeBlockStart()
    {
        var start = Utc(8, 7);
        var end = Utc(8, 10);

        var blocks = _sut.ApplyRule(start, end);

        foreach (var (blockStart, blockEnd) in blocks)
        {
            blockEnd.Should().BeAfter(blockStart);
        }
    }

    [Fact]
    public void ApplyRule_WhenTripIs45Min_ReturnsThreeBlocks()
    {
        var start = Utc(9, 0);
        var end = Utc(9, 45);

        var blocks = _sut.ApplyRule(start, end);

        blocks.Should().HaveCount(3);
    }
}
