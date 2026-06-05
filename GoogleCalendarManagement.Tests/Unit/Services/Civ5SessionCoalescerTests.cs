using FluentAssertions;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class Civ5SessionCoalescerTests
{
    private static Civ5SessionPoint MakePoint(DateTime fileModifiedAt, string gameMode = "single") =>
        new() { FileModifiedAt = fileModifiedAt, GameMode = gameMode, ScannedAt = DateTime.UtcNow };

    // ---------------------------------------------------------------------------
    // Basic coalescing
    // ---------------------------------------------------------------------------

    [Fact]
    public void CoalesceIntoWindows_EmptyInput_ReturnsEmpty()
    {
        var result = Civ5SessionCoalescer.CoalesceIntoWindows([]);
        result.Should().BeEmpty();
    }

    [Fact]
    public void CoalesceIntoWindows_SinglePoint_ReturnsSingleWindow()
    {
        var t = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var result = Civ5SessionCoalescer.CoalesceIntoWindows([MakePoint(t)]);

        result.Should().HaveCount(1);
        result[0].WindowStart.Should().Be(t);
        result[0].WindowEnd.Should().Be(t);
    }

    [Fact]
    public void CoalesceIntoWindows_TwoPointsWithinGap_MergesIntoOneWindow()
    {
        var t1 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var t2 = t1.AddMinutes(29); // 29 min gap = within 30 → merge

        var result = Civ5SessionCoalescer.CoalesceIntoWindows([MakePoint(t1), MakePoint(t2)]);

        result.Should().HaveCount(1);
        result[0].WindowStart.Should().Be(t1);
        result[0].WindowEnd.Should().Be(t2);
    }

    [Fact]
    public void CoalesceIntoWindows_TwoPointsAtExactlyGap_SplitsIntoTwoWindows()
    {
        // AC: "extend while next point is WITHIN 30 minutes" → exactly 30 min = NOT within → new window
        var t1 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var t2 = t1.AddMinutes(30); // exactly 30 min = boundary → splits

        var result = Civ5SessionCoalescer.CoalesceIntoWindows([MakePoint(t1), MakePoint(t2)]);

        result.Should().HaveCount(2);
        result[0].WindowStart.Should().Be(t1);
        result[1].WindowStart.Should().Be(t2);
    }

    [Fact]
    public void CoalesceIntoWindows_TwoPointsBeyondGap_SplitsIntoTwoWindows()
    {
        var t1 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var t2 = t1.AddMinutes(31);

        var result = Civ5SessionCoalescer.CoalesceIntoWindows([MakePoint(t1), MakePoint(t2)]);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void CoalesceIntoWindows_ThreePointsInOneCluster_ReturnsSingleWindow()
    {
        var t0 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var points = new[]
        {
            MakePoint(t0),
            MakePoint(t0.AddMinutes(10)),
            MakePoint(t0.AddMinutes(25))
        };

        var result = Civ5SessionCoalescer.CoalesceIntoWindows(points);

        result.Should().HaveCount(1);
        result[0].WindowStart.Should().Be(t0);
        result[0].WindowEnd.Should().Be(t0.AddMinutes(25));
    }

    [Fact]
    public void CoalesceIntoWindows_UnsortedInput_SortsBeforeCoalescing()
    {
        var t0 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var points = new[]
        {
            MakePoint(t0.AddMinutes(10)),
            MakePoint(t0),                // out of order
            MakePoint(t0.AddMinutes(20))
        };

        var result = Civ5SessionCoalescer.CoalesceIntoWindows(points);

        result.Should().HaveCount(1);
        result[0].WindowStart.Should().Be(t0);
        result[0].WindowEnd.Should().Be(t0.AddMinutes(20));
    }

    // ---------------------------------------------------------------------------
    // Mixed-mode detection
    // ---------------------------------------------------------------------------

    [Fact]
    public void CoalesceIntoWindows_SingleModeWindow_HasOneModeInList()
    {
        var t0 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var points = new[]
        {
            MakePoint(t0, "single"),
            MakePoint(t0.AddMinutes(10), "single")
        };

        var result = Civ5SessionCoalescer.CoalesceIntoWindows(points);

        result[0].GameModes.Should().ContainSingle().Which.Should().Be("single");
    }

    [Fact]
    public void CoalesceIntoWindows_MixedModesInWindow_HasMultipleModes()
    {
        var t0 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var points = new[]
        {
            MakePoint(t0, "single"),
            MakePoint(t0.AddMinutes(10), "multi")
        };

        var result = Civ5SessionCoalescer.CoalesceIntoWindows(points);

        result[0].GameModes.Should().HaveCount(2);
    }

    [Fact]
    public void GetEventTitle_SingleMode_ReturnsCiv5()
    {
        var window = new Civ5CandidateWindow(DateTime.UtcNow, DateTime.UtcNow, ["single"]);
        Civ5SessionCoalescer.GetEventTitle(window).Should().Be("Civ 5");
    }

    [Fact]
    public void GetEventTitle_MixedModes_ReturnsCiv5Mixed()
    {
        var window = new Civ5CandidateWindow(DateTime.UtcNow, DateTime.UtcNow, ["single", "multi"]);
        Civ5SessionCoalescer.GetEventTitle(window).Should().Be("Civ 5 (mixed)");
    }

    // ---------------------------------------------------------------------------
    // Custom gap
    // ---------------------------------------------------------------------------

    [Fact]
    public void CoalesceIntoWindows_CustomGap_UsesProvidedGap()
    {
        var t0 = new DateTime(2025, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddMinutes(10); // within default 30 but within custom 5? No — 10 > 5 → splits

        var result = Civ5SessionCoalescer.CoalesceIntoWindows(
            [MakePoint(t0), MakePoint(t1)],
            coalesceGap: TimeSpan.FromMinutes(5));

        result.Should().HaveCount(2);
    }
}
