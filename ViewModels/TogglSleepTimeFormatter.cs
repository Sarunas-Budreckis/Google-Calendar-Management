using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.ViewModels;

internal static class TogglSleepTimeFormatter
{
    public static string FormatTime(DateTime value)
    {
        return ToLocal(value).ToString("h:mm tt");
    }

    public static string FormatEndTime(DateTime start, DateTime end)
    {
        var localStart = ToLocal(start);
        var localEnd = ToLocal(end);
        var dayDelta = (localEnd.Date - localStart.Date).Days;
        return dayDelta > 0
            ? $"{localEnd:h:mm tt} +{dayDelta}"
            : localEnd.ToString("h:mm tt");
    }

    public static string FormatDuration(TogglEntry entry)
    {
        var duration = entry.EndTime is { } end
            ? ToLocal(end) - ToLocal(entry.StartTime)
            : TimeSpan.FromSeconds(Math.Max(0, entry.DurationSeconds ?? 0));

        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        var totalHours = (int)duration.TotalHours;
        return totalHours > 0
            ? $"{totalHours}h {duration.Minutes}m"
            : $"{duration.Minutes}m";
    }

    public static DateTime ToLocal(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Local => value,
            DateTimeKind.Utc => value.ToLocalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
        };
    }
}
