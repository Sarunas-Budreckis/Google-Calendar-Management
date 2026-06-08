using FluentAssertions;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class ComfyUISessionCoalescerTests
{
    private static ComfyUIScanPoint MakePoint(DateTime fileModifiedAt) =>
        new() { Timestamp = fileModifiedAt, EventType = "modified", ScannedAt = DateTime.UtcNow };

    // ---------------------------------------------------------------------------
    // Basic coalescing
    // ---------------------------------------------------------------------------

    [Fact]
    public void CoalesceIntoWindows_EmptyInput_ReturnsEmpty()
    {
        var result = ComfyUISessionCoalescer.CoalesceIntoWindows([]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void CoalesceIntoWindows_SinglePoint_ReturnsSingleWindow()
    {
        var t = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var result = ComfyUISessionCoalescer.CoalesceIntoWindows([MakePoint(t)]);

        result.Should().HaveCount(1);
        result[0].WindowStart.Should().Be(t);
        result[0].WindowEnd.Should().Be(t);
    }

    [Fact]
    public void CoalesceIntoWindows_TwoPointsWithinGap_MergesIntoOneWindow()
    {
        var t1 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var t2 = t1.AddMinutes(14); // 14 min < 15 → merge

        var result = ComfyUISessionCoalescer.CoalesceIntoWindows([MakePoint(t1), MakePoint(t2)]);

        result.Should().HaveCount(1);
        result[0].WindowStart.Should().Be(t1);
        result[0].WindowEnd.Should().Be(t2);
    }

    [Fact]
    public void CoalesceIntoWindows_TwoPointsAtExactlyGap_SplitsIntoTwoWindows()
    {
        // "extend while WITHIN 15 minutes" → exactly 15 min = NOT within → new window
        var t1 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var t2 = t1.AddMinutes(15);

        var result = ComfyUISessionCoalescer.CoalesceIntoWindows([MakePoint(t1), MakePoint(t2)]);

        result.Should().HaveCount(2);
        result[0].WindowStart.Should().Be(t1);
        result[1].WindowStart.Should().Be(t2);
    }

    [Fact]
    public void CoalesceIntoWindows_TwoPointsBeyondGap_SplitsIntoTwoWindows()
    {
        var t1 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var t2 = t1.AddMinutes(16);

        var result = ComfyUISessionCoalescer.CoalesceIntoWindows([MakePoint(t1), MakePoint(t2)]);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void CoalesceIntoWindows_ThreePointsInOneCluster_ReturnsSingleWindow()
    {
        var t0 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var points = new[]
        {
            MakePoint(t0),
            MakePoint(t0.AddMinutes(5)),
            MakePoint(t0.AddMinutes(12))
        };

        var result = ComfyUISessionCoalescer.CoalesceIntoWindows(points);

        result.Should().HaveCount(1);
        result[0].WindowStart.Should().Be(t0);
        result[0].WindowEnd.Should().Be(t0.AddMinutes(12));
    }

    [Fact]
    public void CoalesceIntoWindows_ThreePointsInTwoClusters_ReturnsTwoWindows()
    {
        var t0 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var points = new[]
        {
            MakePoint(t0),
            MakePoint(t0.AddMinutes(10)),
            MakePoint(t0.AddMinutes(30)) // 20 min after second → new window
        };

        var result = ComfyUISessionCoalescer.CoalesceIntoWindows(points);

        result.Should().HaveCount(2);
        result[0].WindowStart.Should().Be(t0);
        result[0].WindowEnd.Should().Be(t0.AddMinutes(10));
        result[1].WindowStart.Should().Be(t0.AddMinutes(30));
    }

    [Fact]
    public void CoalesceIntoWindows_UnsortedInput_SortsBeforeCoalescing()
    {
        var t0 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var points = new[]
        {
            MakePoint(t0.AddMinutes(10)),
            MakePoint(t0),
            MakePoint(t0.AddMinutes(5))
        };

        var result = ComfyUISessionCoalescer.CoalesceIntoWindows(points);

        result.Should().HaveCount(1);
        result[0].WindowStart.Should().Be(t0);
        result[0].WindowEnd.Should().Be(t0.AddMinutes(10));
    }

    [Fact]
    public void CoalesceIntoWindows_WindowEndExtendsBeyondFirstPoint()
    {
        var t0 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(8);
        var t2 = t1.AddMinutes(8); // 8 min after t1, still within 15 of t1

        var result = ComfyUISessionCoalescer.CoalesceIntoWindows([MakePoint(t0), MakePoint(t1), MakePoint(t2)]);

        result.Should().HaveCount(1);
        result[0].WindowEnd.Should().Be(t2);
    }

    // ---------------------------------------------------------------------------
    // Custom gap
    // ---------------------------------------------------------------------------

    [Fact]
    public void CoalesceIntoWindows_CustomGap_UsesProvidedGap()
    {
        var t0 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(5); // within default 15 but beyond custom 3 → splits

        var result = ComfyUISessionCoalescer.CoalesceIntoWindows(
            [MakePoint(t0), MakePoint(t1)],
            coalesceGap: TimeSpan.FromMinutes(3));

        result.Should().HaveCount(2);
    }
}
