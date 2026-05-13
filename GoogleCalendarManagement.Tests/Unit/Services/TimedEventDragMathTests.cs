using FluentAssertions;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class TimedEventDragMathTests
{
    [Theory]
    [InlineData(94, 100, TimedEventPointerMode.Resize)]
    [InlineData(93, 100, TimedEventPointerMode.Move)]
    public void GetPointerMode_UsesOnlyBottomResizeBoundary(
        double pointerY,
        double eventHeight,
        TimedEventPointerMode expectedMode)
    {
        var mode = TimedEventDragMath.GetPointerMode(pointerY, eventHeight, resizeBoundaryThickness: 6);

        mode.Should().Be(expectedMode);
    }

    [Fact]
    public void GetMovePreview_SnapsToQuarterHourAndPreservesDuration()
    {
        var start = new DateTime(2026, 5, 11, 9, 0, 0);
        var end = start.AddMinutes(50);

        var preview = TimedEventDragMath.GetMovePreview(start, end, rawMinuteDelta: 22);

        preview.StartLocal.Should().Be(start.AddMinutes(15));
        preview.EndLocal.Should().Be(end.AddMinutes(15));
        (preview.EndLocal - preview.StartLocal).Should().Be(TimeSpan.FromMinutes(50));
    }

    [Fact]
    public void GetMovePreview_VerticalOverflowWrapsToPreviousDayVisualSlot()
    {
        var start = new DateTime(2026, 5, 12, 9, 0, 0);
        var end = start.AddHours(1);

        var preview = TimedEventDragMath.GetMovePreview(start, end, rawMinuteDelta: -10 * 60);

        preview.StartLocal.Should().Be(new DateTime(2026, 5, 11, 23, 0, 0));
        preview.EndLocal.Should().Be(new DateTime(2026, 5, 12, 0, 0, 0));
        preview.VisualDayDelta.Should().Be(-1);
        preview.VisualMinuteDelta.Should().Be(14 * 60);
    }

    [Fact]
    public void GetMovePreview_ColumnDeltaMovesToAdjacentDay()
    {
        var start = new DateTime(2026, 5, 12, 9, 0, 0);
        var end = start.AddHours(1);

        var preview = TimedEventDragMath.GetMovePreview(start, end, rawMinuteDelta: 0, dayDelta: 1);

        preview.StartLocal.Should().Be(new DateTime(2026, 5, 13, 9, 0, 0));
        preview.EndLocal.Should().Be(new DateTime(2026, 5, 13, 10, 0, 0));
        preview.VisualDayDelta.Should().Be(1);
        preview.VisualMinuteDelta.Should().Be(0);
    }
}
