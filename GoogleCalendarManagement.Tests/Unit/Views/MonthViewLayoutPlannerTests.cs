using FluentAssertions;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Views;

namespace GoogleCalendarManagement.Tests.Unit.Views;

public sealed class MonthViewLayoutPlannerTests
{
    [Fact]
    public void BuildWeekLayout_UsesAvailableVerticalSpaceForTimedRows()
    {
        var weekStart = new DateOnly(2026, 4, 6);
        var targetDate = new DateOnly(2026, 4, 7);
        var layout = MonthViewLayoutPlanner.BuildWeekLayout(
            weekStart,
            [
                CreateAllDayEvent("all-day-1", "All Day 1", targetDate, targetDate.AddDays(1)),
                CreateAllDayEvent("all-day-2", "All Day 2", targetDate, targetDate.AddDays(1)),
                CreateAllDayEvent("all-day-3", "All Day 3", targetDate, targetDate.AddDays(1)),
                CreateTimedEvent("timed-1", "Morning", targetDate, 9, 0),
                CreateTimedEvent("timed-2", "Late Morning", targetDate, 11, 0),
                CreateTimedEvent("timed-3", "Afternoon", targetDate, 15, 0),
                CreateTimedEvent("timed-4", "Evening", targetDate, 18, 0)
            ]);

        var dayLayout = layout.DayLayouts[targetDate];

        dayLayout.VisibleAllDayEvents.Should().HaveCount(3);
        dayLayout.VisibleTimedEvents.Select(item => item.Title).Should().Equal("Morning", "Late Morning");
        dayLayout.OverflowCount.Should().Be(2);
        dayLayout.OrderedEvents.Take(3).Should().OnlyContain(item => item.IsAllDay);
    }

    [Fact]
    public void BuildWeekLayout_KeepsAtLeastOneTimedRowVisibleWhenHiddenAllDayEventsForceOverflow()
    {
        var weekStart = new DateOnly(2026, 4, 6);
        var targetDate = new DateOnly(2026, 4, 7);
        var layout = MonthViewLayoutPlanner.BuildWeekLayout(
            weekStart,
            [
                CreateAllDayEvent("all-day-1", "All Day 1", targetDate, targetDate.AddDays(1)),
                CreateAllDayEvent("all-day-2", "All Day 2", targetDate, targetDate.AddDays(1)),
                CreateAllDayEvent("all-day-3", "All Day 3", targetDate, targetDate.AddDays(1)),
                CreateAllDayEvent("all-day-4", "All Day 4", targetDate, targetDate.AddDays(1)),
                CreateTimedEvent("timed-1", "Morning", targetDate, 9, 0)
            ]);

        var dayLayout = layout.DayLayouts[targetDate];

        dayLayout.VisibleAllDayEvents.Should().HaveCount(3);
        dayLayout.VisibleTimedEvents.Select(item => item.Title).Should().Equal("Morning");
        dayLayout.OverflowCount.Should().Be(1);
    }

    [Fact]
    public void BuildWeekLayout_ConvertsExclusiveAllDayEndIntoInclusiveSpan()
    {
        var weekStart = new DateOnly(2026, 4, 6);
        var layout = MonthViewLayoutPlanner.BuildWeekLayout(
            weekStart,
            [
                CreateAllDayEvent(
                    "span",
                    "Conference",
                    new DateOnly(2026, 4, 7),
                    new DateOnly(2026, 4, 10))
            ]);

        layout.VisibleAllDayTracks.Should().ContainSingle();
        layout.VisibleAllDayTracks[0].ColumnStart.Should().Be(1);
        layout.VisibleAllDayTracks[0].ColumnEnd.Should().Be(3);
    }

    [Fact]
    public void BuildWeekLayout_SortsTimedEventsByAscendingStartTime()
    {
        var weekStart = new DateOnly(2026, 4, 6);
        var targetDate = new DateOnly(2026, 4, 8);
        var layout = MonthViewLayoutPlanner.BuildWeekLayout(
            weekStart,
            [
                CreateTimedEvent("timed-3", "Three PM", targetDate, 15, 0),
                CreateTimedEvent("timed-1", "Nine AM", targetDate, 9, 0),
                CreateTimedEvent("timed-2", "Eleven AM", targetDate, 11, 0)
            ]);

        layout.DayLayouts[targetDate].TimedEvents
            .Select(item => item.Title)
            .Should()
            .Equal("Nine AM", "Eleven AM", "Three PM");
    }

    [Fact]
    public void BuildWeekLayout_LeavesEmptyDaysWithoutVisibleEventsOrOverflow()
    {
        var weekStart = new DateOnly(2026, 4, 6);
        var emptyDate = new DateOnly(2026, 4, 10);
        var layout = MonthViewLayoutPlanner.BuildWeekLayout(
            weekStart,
            [
                CreateTimedEvent("timed-1", "Only Event", new DateOnly(2026, 4, 7), 9, 0)
            ]);

        var dayLayout = layout.DayLayouts[emptyDate];

        dayLayout.AllDayEvents.Should().BeEmpty();
        dayLayout.TimedEvents.Should().BeEmpty();
        dayLayout.VisibleTimedEvents.Should().BeEmpty();
        dayLayout.OverflowCount.Should().Be(0);
    }

    private static CalendarEventDisplayModel CreateAllDayEvent(
        string id,
        string title,
        DateOnly startDate,
        DateOnly exclusiveEndDate)
    {
        var start = startDate.ToDateTime(TimeOnly.MinValue);
        var end = exclusiveEndDate.ToDateTime(TimeOnly.MinValue);
        return new CalendarEventDisplayModel(
            id,
            title,
            DateTime.SpecifyKind(start, DateTimeKind.Utc),
            DateTime.SpecifyKind(end, DateTimeKind.Utc),
            start,
            end,
            true,
            "#0088CC",
            "Azure",
            false,
            null,
            null);
    }

    private static CalendarEventDisplayModel CreateTimedEvent(
        string id,
        string title,
        DateOnly date,
        int hour,
        int minute)
    {
        var start = date.ToDateTime(new TimeOnly(hour, minute));
        var end = start.AddHours(1);
        return new CalendarEventDisplayModel(
            id,
            title,
            DateTime.SpecifyKind(start, DateTimeKind.Utc),
            DateTime.SpecifyKind(end, DateTimeKind.Utc),
            start,
            end,
            false,
            "#33B679",
            "Navy",
            false,
            null,
            null);
    }
}
