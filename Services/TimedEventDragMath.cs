namespace GoogleCalendarManagement.Services;

public enum TimedEventPointerMode
{
    Move,
    Resize
}

public readonly record struct TimedEventMovePreview(
    DateTime StartLocal,
    DateTime EndLocal,
    int TotalMinuteDelta,
    int VisualMinuteDelta,
    int VisualDayDelta);

public static class TimedEventDragMath
{
    public static TimedEventPointerMode GetPointerMode(double pointerY, double eventHeight, double resizeBoundaryThickness)
    {
        return pointerY >= Math.Max(0, eventHeight - resizeBoundaryThickness)
            ? TimedEventPointerMode.Resize
            : TimedEventPointerMode.Move;
    }

    public static TimedEventMovePreview GetMovePreview(
        DateTime originalStartLocal,
        DateTime originalEndLocal,
        double rawMinuteDelta,
        int dayDelta = 0)
    {
        var totalMinuteDelta = SnapMinutes(rawMinuteDelta) + (dayDelta * 24 * 60);
        var startLocal = originalStartLocal.AddMinutes(totalMinuteDelta);
        var endLocal = originalEndLocal.AddMinutes(totalMinuteDelta);
        var visualDayDelta = (startLocal.Date - originalStartLocal.Date).Days;
        var visualMinuteDelta = (int)Math.Round((startLocal.TimeOfDay - originalStartLocal.TimeOfDay).TotalMinutes);

        return new TimedEventMovePreview(
            startLocal,
            endLocal,
            totalMinuteDelta,
            visualMinuteDelta,
            visualDayDelta);
    }

    public static DateTime GetResizeEndPreview(DateTime originalStartLocal, DateTime originalEndLocal, double rawMinuteDelta)
    {
        var candidateEnd = RoundToNearestQuarterHour(originalEndLocal.AddMinutes(SnapMinutes(rawMinuteDelta)));
        var minEnd = originalStartLocal.AddMinutes(15);
        var maxEnd = originalStartLocal.Date.AddDays(1).AddHours(2);

        return candidateEnd < minEnd
            ? minEnd
            : candidateEnd > maxEnd
                ? maxEnd
                : candidateEnd;
    }

    public static int SnapMinutes(double rawMinutes)
    {
        return (int)Math.Round(rawMinutes / 15.0) * 15;
    }

    public static DateTime RoundToNearestQuarterHour(DateTime value)
    {
        var totalMinutes = value.Hour * 60 + value.Minute + (value.Second / 60.0);
        var snappedMinutes = (int)Math.Round(totalMinutes / 15.0) * 15;
        var dayOffset = Math.DivRem(snappedMinutes, 24 * 60, out var minuteOfDay);
        if (minuteOfDay < 0)
        {
            minuteOfDay += 24 * 60;
            dayOffset--;
        }

        return value.Date.AddDays(dayOffset).AddMinutes(minuteOfDay);
    }
}
