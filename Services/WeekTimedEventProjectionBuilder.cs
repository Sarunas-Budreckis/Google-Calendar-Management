using System.Globalization;
using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Services;

public static class WeekTimedEventProjectionBuilder
{
    private const double TimeColumnWidth = 72;
    private const double WeekGridHorizontalPadding = 24.0;
    private const double RowHeight = 72.0;
    private const double EventBottomGap = 3.0;
    private const double EventSideMargin = 4.0;
    private const double OverlapIndent = 10.0;
    private const double MinimumEventHeight = 15.0;
    private const double StandardTopPadding = 6.0;
    private const double ShortEventContentHeightEstimate = 16.0;

    public static IReadOnlyList<WeekTimedEventLayoutItem> Build(
        DateOnly weekStart,
        IEnumerable<CalendarEventDisplayModel> currentEvents,
        double dayColumnWidth,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(currentEvents);
        ArgumentNullException.ThrowIfNull(culture);

        var layoutItems = new List<WeekTimedEventLayoutItem>();

        for (var offset = 0; offset < 7; offset++)
        {
            var currentDay = weekStart.AddDays(offset);
            var dayStart = currentDay.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);

            var timedSegments = BuildTimedEventSegments(
                dayStart,
                dayEnd,
                currentEvents
                    .Where(evt => !evt.IsAllDay && evt.StartLocal < dayEnd && evt.EndLocal > dayStart)
                    .OrderBy(evt => evt.StartLocal)
                    .ThenBy(evt => evt.EndLocal));

            foreach (var segment in timedSegments)
            {
                layoutItems.Add(CreateLayoutItem(segment, offset, dayColumnWidth, culture));
            }
        }

        return layoutItems;
    }

    private static WeekTimedEventLayoutItem CreateLayoutItem(
        TimedEventSegment segment,
        int dayOffset,
        double dayColumnWidth,
        CultureInfo culture)
    {
        var durationMinutes = (segment.VisibleEnd - segment.VisibleStart).TotalMinutes;
        var minutesFromDayStart = (segment.VisibleStart - segment.DayStart).TotalMinutes;
        var top = minutesFromDayStart / 60.0 * RowHeight;
        var pixelHeight = durationMinutes / 60.0 * RowHeight;
        var eventHeight = Math.Max(MinimumEventHeight, pixelHeight - EventBottomGap);
        var left = (WeekGridHorizontalPadding / 2d) + TimeColumnWidth + (dayOffset * dayColumnWidth) + EventSideMargin + (segment.OverlapDepth * OverlapIndent);
        var width = Math.Max(1d, dayColumnWidth - (2 * EventSideMargin) - (segment.OverlapDepth * OverlapIndent));
        var gridRow = Math.Clamp((int)Math.Floor(minutesFromDayStart / 60.0), 0, 23);
        var minutesIntoStartHour = minutesFromDayStart - (gridRow * 60.0);
        var totalMinutesFromStartHour = minutesIntoStartHour + durationMinutes;
        var gridRowSpan = (int)Math.Ceiling(totalMinutesFromStartHour / 60.0);
        var displayStart = GetDisplayStart(segment);
        var displayEnd = GetDisplayEnd(segment);

        if (durationMinutes < 45)
        {
            var centeredTopPadding = Math.Max(0, (eventHeight - ShortEventContentHeightEstimate) / 2);
            return new WeekTimedEventLayoutItem(
                segment.Item.GcalEventId,
                segment.Item.Title,
                $"{segment.Item.Title}, {displayStart.ToString("t", culture)}",
                null,
                BuildTooltipText(segment.Item, culture),
                segment.Item.ColorHex,
                dayOffset,
                gridRow,
                Math.Max(1, Math.Min(gridRowSpan, 24 - gridRow)),
                left,
                top,
                width,
                eventHeight,
                Math.Min(StandardTopPadding, centeredTopPadding),
                true,
                segment.OverlapDepth > 0,
                1);
        }

        var durationInt = (int)durationMinutes;
        var summaryLineCount = 1 + Math.Max(0, (durationInt - 60) / 30);

        return new WeekTimedEventLayoutItem(
            segment.Item.GcalEventId,
            segment.Item.Title,
            segment.Item.Title,
            $"{displayStart.ToString("t", culture)} - {displayEnd.ToString("t", culture)}",
            BuildTooltipText(segment.Item, culture),
            segment.Item.ColorHex,
            dayOffset,
            gridRow,
            Math.Max(1, Math.Min(gridRowSpan, 24 - gridRow)),
            left,
            top,
            width,
            eventHeight,
            StandardTopPadding,
            false,
            segment.OverlapDepth > 0,
            summaryLineCount);
    }

    private static string BuildTooltipText(CalendarEventDisplayModel item, CultureInfo culture)
    {
        return item.IsAllDay
            ? $"{item.Title}\nAll day"
            : $"{item.Title}\n{item.StartLocal.ToString("g", culture)} - {item.EndLocal.ToString("g", culture)}";
    }

    private static DateTime GetDisplayStart(TimedEventSegment segment)
    {
        return segment.Item.StartLocal.Date != segment.Item.EndLocal.Date
            ? segment.Item.StartLocal
            : segment.VisibleStart;
    }

    private static DateTime GetDisplayEnd(TimedEventSegment segment)
    {
        return segment.Item.StartLocal.Date != segment.Item.EndLocal.Date
            ? segment.Item.EndLocal
            : segment.VisibleEnd;
    }

    private static List<TimedEventSegment> BuildTimedEventSegments(
        DateTime dayStart,
        DateTime dayEnd,
        IEnumerable<CalendarEventDisplayModel> events)
    {
        var segments = new List<TimedEventSegment>();
        var activeSegments = new List<DateTime>();

        foreach (var candidate in events
                     .Select(item =>
                     {
                         var visibleStart = item.StartLocal > dayStart ? item.StartLocal : dayStart;
                         var visibleEnd = item.EndLocal < dayEnd ? item.EndLocal : dayEnd;
                         return new TimedEventSegment(item, dayStart, visibleStart, visibleEnd, 0);
                     })
                     .Where(segment => segment.VisibleEnd > segment.VisibleStart)
                     .OrderBy(segment => segment.VisibleStart)
                     .ThenBy(segment => segment.VisibleEnd))
        {
            activeSegments.RemoveAll(end => end <= candidate.VisibleStart);
            var overlapDepth = activeSegments.Count;
            activeSegments.Add(candidate.VisibleEnd);
            segments.Add(candidate with { OverlapDepth = overlapDepth });
        }

        return segments;
    }

    private sealed record TimedEventSegment(
        CalendarEventDisplayModel Item,
        DateTime DayStart,
        DateTime VisibleStart,
        DateTime VisibleEnd,
        int OverlapDepth);
}
