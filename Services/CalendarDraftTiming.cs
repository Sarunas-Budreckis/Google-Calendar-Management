namespace GoogleCalendarManagement.Services;

public static class CalendarDraftTiming
{
    public static DateTime RoundToNearestQuarterHour(DateTime value)
    {
        var totalMinutes = value.Hour * 60 + value.Minute + (value.Second / 60.0);
        var snappedMinutes = (int)Math.Round(totalMinutes / 15.0, MidpointRounding.AwayFromZero) * 15;
        var dayOffset = Math.DivRem(snappedMinutes, 24 * 60, out var minuteOfDay);
        if (minuteOfDay < 0)
        {
            minuteOfDay += 24 * 60;
            dayOffset--;
        }

        return value.Date.AddDays(dayOffset).AddMinutes(minuteOfDay);
    }

    public static (DateTime StartLocal, DateTime EndLocal) GetButtonDefaults(TimeProvider timeProvider)
    {
        var localNow = timeProvider.GetLocalNow().DateTime;
        var roundedStart = RoundToNearestQuarterHour(localNow);
        return (roundedStart, roundedStart.AddHours(1));
    }

    public static (DateTime StartLocal, DateTime EndLocal) SnapDragRange(DateTime anchorLocal, DateTime currentLocal)
    {
        var snappedStart = RoundDownToQuarterHour(anchorLocal);
        var snappedEnd = RoundUpToQuarterHour(currentLocal);

        if (snappedEnd < snappedStart)
        {
            (snappedStart, snappedEnd) = (RoundDownToQuarterHour(currentLocal), RoundUpToQuarterHour(anchorLocal));
        }

        if (snappedEnd <= snappedStart)
        {
            snappedEnd = snappedStart.AddMinutes(15);
        }

        return (snappedStart, snappedEnd);
    }

    public static DateTime ClampToDay(DateTime value, DateOnly day)
    {
        var dayStart = day.ToDateTime(TimeOnly.MinValue);
        var dayEnd = dayStart.AddDays(1);
        if (value < dayStart)
        {
            return dayStart;
        }

        return value > dayEnd ? dayEnd : value;
    }

    private static DateTime RoundDownToQuarterHour(DateTime value)
    {
        var totalMinutes = value.Hour * 60 + value.Minute;
        var snappedMinutes = (int)Math.Floor(totalMinutes / 15d) * 15;
        return value.Date.AddMinutes(snappedMinutes);
    }

    private static DateTime RoundUpToQuarterHour(DateTime value)
    {
        var totalMinutes = value.Hour * 60 + value.Minute;
        var snappedMinutes = (int)Math.Ceiling(totalMinutes / 15d) * 15;
        return value.Date.AddMinutes(snappedMinutes);
    }
}
