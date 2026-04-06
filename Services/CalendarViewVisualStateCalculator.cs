using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Services;

public static class CalendarViewVisualStateCalculator
{
    private const double MinutesPerDay = 24d * 60d;

    public static bool IsToday(DateOnly date, DateTime localNow)
    {
        return date == DateOnly.FromDateTime(localNow);
    }

    public static bool TryGetCurrentTimeIndicatorTop(
        DateOnly visibleDate,
        DateTime localNow,
        double timelineHeight,
        out double topOffset)
    {
        if (!IsToday(visibleDate, localNow))
        {
            topOffset = 0;
            return false;
        }

        topOffset = (localNow.TimeOfDay.TotalMinutes / MinutesPerDay) * timelineHeight;
        return true;
    }

    public static bool TryClipTimedEventToDay(
        CalendarEventDisplayModel calendarEvent,
        DateOnly visibleDate,
        out VisibleTimedEventSegment segment)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        segment = default;
        if (calendarEvent.IsAllDay)
        {
            return false;
        }

        var dayStart = visibleDate.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);

        if (calendarEvent.EndLocal <= dayStart || calendarEvent.StartLocal >= dayEnd)
        {
            return false;
        }

        var visibleStart = calendarEvent.StartLocal > dayStart
            ? calendarEvent.StartLocal
            : dayStart;
        var visibleEnd = calendarEvent.EndLocal < dayEnd
            ? calendarEvent.EndLocal
            : dayEnd;

        if (visibleEnd <= visibleStart)
        {
            return false;
        }

        segment = new VisibleTimedEventSegment(
            calendarEvent.GcalEventId,
            calendarEvent.Title,
            calendarEvent.ColorHex,
            calendarEvent.StartLocal,
            calendarEvent.EndLocal,
            visibleStart,
            visibleEnd);
        return true;
    }
}

public readonly record struct VisibleTimedEventSegment(
    string GcalEventId,
    string Title,
    string ColorHex,
    DateTime OriginalStart,
    DateTime OriginalEnd,
    DateTime VisibleStart,
    DateTime VisibleEnd);
