using System.Globalization;

namespace GoogleCalendarManagement.Services;

public static class RelativeTimeFormatter
{
    public static string FormatElapsed(DateTime timestamp, DateTime now)
    {
        var localTimestamp = timestamp.ToLocalTime();
        var elapsed = now - localTimestamp;

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            var minutes = Math.Max(1, (int)elapsed.TotalMinutes);
            return $"{minutes} minute{(minutes == 1 ? string.Empty : "s")} ago";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            var hours = Math.Max(1, (int)elapsed.TotalHours);
            return $"{hours} hour{(hours == 1 ? string.Empty : "s")} ago";
        }

        if (elapsed < TimeSpan.FromDays(7))
        {
            var days = Math.Max(1, (int)elapsed.TotalDays);
            return $"{days} day{(days == 1 ? string.Empty : "s")} ago";
        }

        return localTimestamp.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);
    }
}
