using FluentAssertions;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;

namespace GoogleCalendarManagement.Tests.Unit.Services;

public sealed class YearViewDayProjectionBuilderTests
{
    [Fact]
    public void Build_UsesFirstSingleDayAllDayEvent_AndKeepsTrailingSyncDotMetadata()
    {
        var date = new DateOnly(2026, 03, 15);
        var events = new[]
        {
            CreateAllDayEvent("single", "Single Day", date, date, "#112233"),
            CreateTimedEvent("timed", "Timed", date, "#445566")
        };
        var syncStatuses = new Dictionary<DateOnly, SyncStatus>
        {
            [date] = SyncStatus.Synced
        };

        var projection = YearViewDayProjectionBuilder.Build(VisibleDates(date, date), events, syncStatuses);

        projection.DayLookup[date].SyncStatus.Should().Be(SyncStatus.Synced);
        projection.DayLookup[date].SyncDotPlacement.Should().Be(YearViewSyncDotPlacement.Trailing);
        projection.DayLookup[date].SingleDayAllDayBar.HasContent.Should().BeTrue();
        projection.DayLookup[date].SingleDayAllDayBar.ColorHex.Should().Be("#112233");
        projection.DayLookup[date].SingleDayAllDayBar.SummaryText.Should().Be("Single Day");
        projection.DayLookup[date].MultiDayAllDayBar.HasContent.Should().BeFalse();
    }

    [Fact]
    public void Build_TimedEventsDoNotCreateYearViewBars()
    {
        var date = new DateOnly(2026, 04, 02);
        var events = new[]
        {
            CreateTimedEvent("timed-1", "Morning", date, "#AA0000"),
            CreateTimedEvent("timed-2", "Afternoon", date, "#00AA00")
        };

        var projection = YearViewDayProjectionBuilder.Build(VisibleDates(date, date), events, new Dictionary<DateOnly, SyncStatus>());

        projection.DayLookup[date].SingleDayAllDayBar.HasContent.Should().BeFalse();
        projection.DayLookup[date].MultiDayAllDayBar.HasContent.Should().BeFalse();
    }

    [Fact]
    public void Build_LeavesSingleDayBarBlankWhenNoSingleDayAllDayEventExists()
    {
        var date = new DateOnly(2026, 05, 10);
        var events = new[]
        {
            CreateAllDayEvent("multi", "Conference", date, date.AddDays(1), "#336699")
        };

        var projection = YearViewDayProjectionBuilder.Build(VisibleDates(date, date), events, new Dictionary<DateOnly, SyncStatus>());

        projection.DayLookup[date].SingleDayAllDayBar.HasContent.Should().BeFalse();
        projection.DayLookup[date].MultiDayAllDayBar.HasContent.Should().BeTrue();
    }

    [Fact]
    public void Build_ContinuesAlreadyStartedMultiDayEventBeforeLongerCompetitor()
    {
        var start = new DateOnly(2026, 06, 10);
        var events = new[]
        {
            CreateAllDayEvent("carry", "Carry Forward", start, start.AddDays(1), "#123456"),
            CreateAllDayEvent("longer", "Longer Event", start.AddDays(1), start.AddDays(4), "#654321")
        };

        var projection = YearViewDayProjectionBuilder.Build(VisibleDates(start, start.AddDays(2)), events, new Dictionary<DateOnly, SyncStatus>());

        projection.DayLookup[start].MultiDayAllDayBar.GcalEventId.Should().Be("carry");
        projection.DayLookup[start.AddDays(1)].MultiDayAllDayBar.GcalEventId.Should().Be("carry");
        projection.DayLookup[start.AddDays(2)].MultiDayAllDayBar.GcalEventId.Should().Be("longer");
    }

    [Fact]
    public void Build_WhenNoCarryForwardExists_ChoosesLongestEligibleMultiDayEvent()
    {
        var date = new DateOnly(2026, 07, 20);
        var events = new[]
        {
            CreateAllDayEvent("short", "Short", date, date.AddDays(1), "#100000"),
            CreateAllDayEvent("long", "Long", date, date.AddDays(3), "#200000")
        };

        var projection = YearViewDayProjectionBuilder.Build(VisibleDates(date, date), events, new Dictionary<DateOnly, SyncStatus>());

        projection.DayLookup[date].MultiDayAllDayBar.GcalEventId.Should().Be("long");
        projection.DayLookup[date].MultiDayAllDayBar.ColorHex.Should().Be("#200000");
    }

    [Fact]
    public void Build_ShowsSummaryTextForSingleDayAndTwoDayEvents()
    {
        var singleDay = new DateOnly(2026, 08, 05);
        var twoDayStart = new DateOnly(2026, 08, 10);
        var events = new[]
        {
            CreateAllDayEvent("single-day", "Single Day", singleDay, singleDay, "#111111"),
            CreateAllDayEvent("two-day", "Two Day", twoDayStart, twoDayStart.AddDays(1), "#222222")
        };

        var projection = YearViewDayProjectionBuilder.Build(
            VisibleDates(singleDay, twoDayStart.AddDays(1)),
            events,
            new Dictionary<DateOnly, SyncStatus>());

        projection.DayLookup[singleDay].SingleDayAllDayBar.SummaryText.Should().Be("Single Day");
        projection.DayLookup[twoDayStart].MultiDayAllDayBar.SummaryText.Should().Be("Two Day");
    }

    [Fact]
    public void Build_CollapsesMultiDayAssignmentsIntoSingleWeekSegments()
    {
        var monday = new DateOnly(2026, 08, 03);
        var events = new[]
        {
            CreateAllDayEvent("multi", "Multi", monday.AddDays(1), monday.AddDays(3), "#222222")
        };

        var projection = YearViewDayProjectionBuilder.Build(
            VisibleDates(monday, monday.AddDays(6)),
            events,
            new Dictionary<DateOnly, SyncStatus>());

        var segments = projection.MultiDaySegmentsByWeekStart[monday];
        segments.Should().ContainSingle();
        segments[0].GcalEventId.Should().Be("multi");
        segments[0].StartColumn.Should().Be(1);
        segments[0].ColumnSpan.Should().Be(3);
        segments[0].Bar.SummaryText.Should().Be("Multi");
    }

    private static IEnumerable<DateOnly> VisibleDates(DateOnly from, DateOnly to)
    {
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static CalendarEventDisplayModel CreateAllDayEvent(
        string id,
        string title,
        DateOnly startDate,
        DateOnly inclusiveEndDate,
        string colorHex)
    {
        return new CalendarEventDisplayModel(
            id,
            title,
            startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            inclusiveEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            startDate.ToDateTime(TimeOnly.MinValue),
            inclusiveEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
            true,
            colorHex,
            "Azure",
            false,
            null,
            null);
    }

    private static CalendarEventDisplayModel CreateTimedEvent(string id, string title, DateOnly date, string colorHex)
    {
        var start = date.ToDateTime(new TimeOnly(9, 0));
        var end = date.ToDateTime(new TimeOnly(10, 0));

        return new CalendarEventDisplayModel(
            id,
            title,
            DateTime.SpecifyKind(start, DateTimeKind.Utc),
            DateTime.SpecifyKind(end, DateTimeKind.Utc),
            start,
            end,
            false,
            colorHex,
            "Azure",
            false,
            null,
            null);
    }
}
