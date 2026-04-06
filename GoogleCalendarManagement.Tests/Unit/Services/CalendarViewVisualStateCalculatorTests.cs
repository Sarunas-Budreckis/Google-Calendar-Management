using FluentAssertions;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class CalendarViewVisualStateCalculatorTests
{
    [Fact]
    public void ClipTimedEventToDay_ReturnsVisibleStartAndEndForStartDay()
    {
        var visibleDate = new DateOnly(2026, 4, 6);
        var calendarEvent = CreateTimedEvent(
            "evt-overnight",
            "Overnight",
            new DateTime(2026, 4, 6, 22, 0, 0),
            new DateTime(2026, 4, 7, 1, 30, 0));

        var result = CalendarViewVisualStateCalculator.TryClipTimedEventToDay(calendarEvent, visibleDate, out var segment);

        result.Should().BeTrue();
        segment.GcalEventId.Should().Be("evt-overnight");
        segment.VisibleStart.Should().Be(new DateTime(2026, 4, 6, 22, 0, 0));
        segment.VisibleEnd.Should().Be(new DateTime(2026, 4, 7, 0, 0, 0));
    }

    [Fact]
    public void ClipTimedEventToDay_ReturnsVisibleStartAndEndForEndDay()
    {
        var visibleDate = new DateOnly(2026, 4, 7);
        var calendarEvent = CreateTimedEvent(
            "evt-overnight",
            "Overnight",
            new DateTime(2026, 4, 6, 22, 0, 0),
            new DateTime(2026, 4, 7, 1, 30, 0));

        var result = CalendarViewVisualStateCalculator.TryClipTimedEventToDay(calendarEvent, visibleDate, out var segment);

        result.Should().BeTrue();
        segment.VisibleStart.Should().Be(new DateTime(2026, 4, 7, 0, 0, 0));
        segment.VisibleEnd.Should().Be(new DateTime(2026, 4, 7, 1, 30, 0));
    }

    [Fact]
    public void ClipTimedEventToDay_ReturnsFalseWhenEventDoesNotOverlapVisibleDate()
    {
        var visibleDate = new DateOnly(2026, 4, 8);
        var calendarEvent = CreateTimedEvent(
            "evt-overnight",
            "Overnight",
            new DateTime(2026, 4, 6, 22, 0, 0),
            new DateTime(2026, 4, 7, 1, 30, 0));

        var result = CalendarViewVisualStateCalculator.TryClipTimedEventToDay(calendarEvent, visibleDate, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void TryGetCurrentTimeIndicatorTop_ReturnsTopOffsetForToday()
    {
        var visibleDate = new DateOnly(2026, 4, 6);
        var localNow = new DateTime(2026, 4, 6, 9, 30, 0);

        var result = CalendarViewVisualStateCalculator.TryGetCurrentTimeIndicatorTop(
            visibleDate,
            localNow,
            timelineHeight: 1728,
            out var topOffset);

        result.Should().BeTrue();
        topOffset.Should().BeApproximately(684, 0.001);
    }

    [Fact]
    public void TryGetCurrentTimeIndicatorTop_ReturnsFalseForNonTodayDate()
    {
        var visibleDate = new DateOnly(2026, 4, 7);
        var localNow = new DateTime(2026, 4, 6, 9, 30, 0);

        var result = CalendarViewVisualStateCalculator.TryGetCurrentTimeIndicatorTop(
            visibleDate,
            localNow,
            timelineHeight: 1728,
            out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsToday_UsesLocalCalendarDate()
    {
        var localNow = new DateTime(2026, 4, 6, 23, 59, 0);

        CalendarViewVisualStateCalculator.IsToday(new DateOnly(2026, 4, 6), localNow).Should().BeTrue();
        CalendarViewVisualStateCalculator.IsToday(new DateOnly(2026, 4, 7), localNow).Should().BeFalse();
    }

    private static CalendarEventDisplayModel CreateTimedEvent(
        string id,
        string title,
        DateTime start,
        DateTime end)
    {
        return new CalendarEventDisplayModel(
            id,
            title,
            DateTime.SpecifyKind(start, DateTimeKind.Utc),
            DateTime.SpecifyKind(end, DateTimeKind.Utc),
            start,
            end,
            false,
            "#336699",
            "Azure",
            false,
            null,
            null);
    }
}
