using FluentAssertions;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class CalendarDraftTimingTests
{
    [Fact]
    public void GetButtonDefaults_RoundsCurrentLocalTimeToNearestQuarterHour_AndAddsOneHour()
    {
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 04, 19, 14, 08, 0, TimeSpan.Zero));

        var (startLocal, endLocal) = CalendarDraftTiming.GetButtonDefaults(timeProvider);

        startLocal.Should().Be(new DateTime(2026, 04, 19, 9, 15, 0));
        endLocal.Should().Be(new DateTime(2026, 04, 19, 10, 15, 0));
    }

    [Fact]
    public void SnapDragRange_RoundsToQuarterHours_AndEnforcesMinimumFifteenMinutes()
    {
        var anchorLocal = new DateTime(2026, 04, 19, 9, 7, 0);
        var currentLocal = new DateTime(2026, 04, 19, 9, 10, 0);

        var (startLocal, endLocal) = CalendarDraftTiming.SnapDragRange(anchorLocal, currentLocal);

        startLocal.Should().Be(new DateTime(2026, 04, 19, 9, 0, 0));
        endLocal.Should().Be(new DateTime(2026, 04, 19, 9, 15, 0));
    }

    [Fact]
    public void SnapDragRange_BackwardDrag_NormalizesToAscendingRange()
    {
        var anchorLocal = new DateTime(2026, 04, 19, 14, 43, 0);
        var currentLocal = new DateTime(2026, 04, 19, 13, 52, 0);

        var (startLocal, endLocal) = CalendarDraftTiming.SnapDragRange(anchorLocal, currentLocal);

        startLocal.Should().Be(new DateTime(2026, 04, 19, 13, 45, 0));
        endLocal.Should().Be(new DateTime(2026, 04, 19, 14, 45, 0));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
