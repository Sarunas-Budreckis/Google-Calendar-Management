using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Views;

public static class MonthViewLayoutPlanner
{
    public const int DefaultMaxVisibleAllDayTracks = 3;

    public static MonthWeekLayout BuildWeekLayout(
        DateOnly weekStart,
        IEnumerable<CalendarEventDisplayModel> events,
        int maxVisibleAllDayTracks = DefaultMaxVisibleAllDayTracks)
    {
        if (maxVisibleAllDayTracks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxVisibleAllDayTracks));
        }

        var weekEnd = weekStart.AddDays(6);
        var normalizedEvents = events
            .Select(Normalize)
            .Where(item => OccursInWeek(item, weekStart, weekEnd))
            .ToList();

        var allDayEvents = normalizedEvents
            .Where(item => item.Event.IsAllDay)
            .OrderBy(item => item.StartDay)
            .ThenByDescending(item => item.EndDay.DayNumber - item.StartDay.DayNumber)
            .ThenBy(item => item.Event.Title)
            .ToList();

        var visibleTracks = BuildVisibleAllDayTracks(allDayEvents, weekStart, weekEnd, maxVisibleAllDayTracks);
        var dayLayouts = new Dictionary<DateOnly, MonthDayLayout>();

        for (var offset = 0; offset < 7; offset++)
        {
            var date = weekStart.AddDays(offset);
            var allDayForDate = allDayEvents
                .Where(item => OccursOnDate(item, date))
                .Select(item => item.Event)
                .ToList();
            var visibleAllDayForDate = visibleTracks
                .Where(track => track.ColumnStart <= offset && track.ColumnEnd >= offset)
                .OrderBy(track => track.TrackIndex)
                .Select(track => track.Event)
                .ToList();
            var timedForDate = normalizedEvents
                .Where(item => !item.Event.IsAllDay && item.StartDay == date)
                .OrderBy(item => item.Event.StartLocal)
                .ThenBy(item => item.Event.Title)
                .Select(item => item.Event)
                .ToList();
            var hiddenAllDayCount = Math.Max(0, allDayForDate.Count - visibleAllDayForDate.Count);
            var visibleTimedCount = CalculateVisibleTimedCount(
                timedForDate.Count,
                hiddenAllDayCount,
                visibleTracks);
            var visibleTimedForDate = timedForDate.Take(visibleTimedCount).ToList();
            var orderedEvents = allDayForDate.Concat(timedForDate).ToList();
            var overflowCount = Math.Max(
                0,
                orderedEvents.Count - visibleAllDayForDate.Count - visibleTimedForDate.Count);

            dayLayouts[date] = new MonthDayLayout(
                date,
                allDayForDate,
                visibleAllDayForDate,
                timedForDate,
                visibleTimedForDate,
                orderedEvents,
                overflowCount);
        }

        return new MonthWeekLayout(weekStart, visibleTracks, dayLayouts);
    }

    private static int CalculateVisibleTimedCount(
        int timedEventCount,
        int hiddenAllDayCount,
        IReadOnlyList<MonthAllDayTrackLayout> visibleTracks)
    {
        if (timedEventCount == 0)
        {
            return 0;
        }

        var visibleTrackCount = visibleTracks.Count == 0
            ? 0
            : visibleTracks.Max(track => track.TrackIndex) + 1;
        var availableTimedAreaHeight = MonthViewLayoutMetrics.WeekRowHeight
            - MonthViewLayoutMetrics.DayHeaderHeight
            - (visibleTrackCount * MonthViewLayoutMetrics.AllDayTrackHeight)
            - MonthViewLayoutMetrics.ContentVerticalChrome;
        var maxTimedWithoutMoreLink = CalculateTimedRowsThatFit(availableTimedAreaHeight, reserveMoreLinkSpace: false);
        var needsMoreLink = hiddenAllDayCount > 0 || timedEventCount > maxTimedWithoutMoreLink;

        if (!needsMoreLink)
        {
            return Math.Min(timedEventCount, maxTimedWithoutMoreLink);
        }

        var maxTimedWithMoreLink = CalculateTimedRowsThatFit(availableTimedAreaHeight, reserveMoreLinkSpace: true);
        return Math.Min(timedEventCount, Math.Max(1, maxTimedWithMoreLink));
    }

    private static int CalculateTimedRowsThatFit(double availableTimedAreaHeight, bool reserveMoreLinkSpace)
    {
        var remainingHeight = availableTimedAreaHeight
            - (reserveMoreLinkSpace ? MonthViewLayoutMetrics.MoreLinkHeight : 0);

        if (remainingHeight < MonthViewLayoutMetrics.TimedRowHeight)
        {
            return 0;
        }

        return 1 + (int)Math.Floor(
            (remainingHeight - MonthViewLayoutMetrics.TimedRowHeight)
            / (MonthViewLayoutMetrics.TimedRowHeight + MonthViewLayoutMetrics.TimedRowSpacing));
    }

    private static List<MonthAllDayTrackLayout> BuildVisibleAllDayTracks(
        IReadOnlyList<NormalizedMonthEvent> allDayEvents,
        DateOnly weekStart,
        DateOnly weekEnd,
        int maxVisibleAllDayTracks)
    {
        var tracks = new List<List<MonthAllDayTrackLayout>>();

        foreach (var allDayEvent in allDayEvents)
        {
            var columnStart = Math.Max(0, allDayEvent.StartDay.DayNumber - weekStart.DayNumber);
            var columnEnd = Math.Min(6, allDayEvent.EndDay.DayNumber - weekStart.DayNumber);
            var continuesFromLeft = allDayEvent.StartDay < weekStart;
            var continuesToRight = allDayEvent.EndDay > weekEnd;
            var placed = false;

            for (var trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            {
                if (tracks[trackIndex][^1].ColumnEnd >= columnStart)
                {
                    continue;
                }

                tracks[trackIndex].Add(new MonthAllDayTrackLayout(
                    allDayEvent.Event,
                    trackIndex,
                    columnStart,
                    columnEnd,
                    continuesFromLeft,
                    continuesToRight));
                placed = true;
                break;
            }

            if (placed || tracks.Count >= maxVisibleAllDayTracks)
            {
                continue;
            }

            tracks.Add(
            [
                new MonthAllDayTrackLayout(
                    allDayEvent.Event,
                    tracks.Count,
                    columnStart,
                    columnEnd,
                    continuesFromLeft,
                    continuesToRight)
            ]);
        }

        return tracks
            .SelectMany(track => track)
            .OrderBy(track => track.TrackIndex)
            .ThenBy(track => track.ColumnStart)
            .ToList();
    }

    private static bool OccursInWeek(NormalizedMonthEvent item, DateOnly weekStart, DateOnly weekEnd)
    {
        return item.Event.IsAllDay
            ? item.StartDay <= weekEnd && item.EndDay >= weekStart
            : item.StartDay >= weekStart && item.StartDay <= weekEnd;
    }

    private static bool OccursOnDate(NormalizedMonthEvent item, DateOnly date)
    {
        return item.Event.IsAllDay
            ? item.StartDay <= date && item.EndDay >= date
            : item.StartDay == date;
    }

    private static NormalizedMonthEvent Normalize(CalendarEventDisplayModel calendarEvent)
    {
        var startDay = DateOnly.FromDateTime(calendarEvent.StartLocal.Date);

        if (!calendarEvent.IsAllDay)
        {
            return new NormalizedMonthEvent(calendarEvent, startDay, startDay);
        }

        var rawEndDay = DateOnly.FromDateTime(calendarEvent.EndLocal.Date);
        var endDay = rawEndDay > startDay
            ? rawEndDay.AddDays(-1)
            : startDay;

        if (endDay < startDay)
        {
            endDay = startDay;
        }

        return new NormalizedMonthEvent(calendarEvent, startDay, endDay);
    }

    private sealed record NormalizedMonthEvent(
        CalendarEventDisplayModel Event,
        DateOnly StartDay,
        DateOnly EndDay);
}

public sealed record MonthWeekLayout(
    DateOnly WeekStart,
    IReadOnlyList<MonthAllDayTrackLayout> VisibleAllDayTracks,
    IReadOnlyDictionary<DateOnly, MonthDayLayout> DayLayouts);

public sealed record MonthAllDayTrackLayout(
    CalendarEventDisplayModel Event,
    int TrackIndex,
    int ColumnStart,
    int ColumnEnd,
    bool ContinuesFromLeft,
    bool ContinuesToRight);

public sealed record MonthDayLayout(
    DateOnly Date,
    IReadOnlyList<CalendarEventDisplayModel> AllDayEvents,
    IReadOnlyList<CalendarEventDisplayModel> VisibleAllDayEvents,
    IReadOnlyList<CalendarEventDisplayModel> TimedEvents,
    IReadOnlyList<CalendarEventDisplayModel> VisibleTimedEvents,
    IReadOnlyList<CalendarEventDisplayModel> OrderedEvents,
    int OverflowCount);

public static class MonthViewLayoutMetrics
{
    public const double WeekRowHeight = 188;
    public const double DayHeaderHeight = 28;
    public const double AllDayTrackHeight = 24;
    public const double TimedRowHeight = 20;
    public const double TimedRowSpacing = 4;
    public const double MoreLinkHeight = 20;
    public const double ContentVerticalChrome = 12;
}
