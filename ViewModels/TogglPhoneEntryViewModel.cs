using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.ViewModels;

public sealed class TogglPhoneEntryViewModel
{
    public TogglPhoneEntryViewModel(string tooltip, double dotTopOffset)
    {
        Tooltip = tooltip;
        DotTopOffset = dotTopOffset;
    }

    public string Tooltip { get; }
    public double DotTopOffset { get; }

    public static TogglPhoneEntryViewModel FromEntry(TogglEntry entry, double canvasHeight)
    {
        var localStart = ToLocal(entry.StartTime);
        var minuteOfDay = localStart.Hour * 60 + localStart.Minute;
        var dotTopOffset = minuteOfDay / (24.0 * 60.0) * canvasHeight;

        var startLabel = localStart.ToString("h:mm tt");
        var endLabel = entry.EndTime.HasValue ? ToLocal(entry.EndTime.Value).ToString("h:mm tt") : "–";
        var duration = entry.DurationSeconds.HasValue
            ? TimeSpan.FromSeconds(entry.DurationSeconds.Value)
            : (entry.EndTime.HasValue ? entry.EndTime.Value - entry.StartTime : TimeSpan.Zero);
        var durationLabel = duration.TotalMinutes >= 60
            ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
            : $"{(int)duration.TotalMinutes}m";
        var description = string.IsNullOrWhiteSpace(entry.Description) ? "(No description)" : entry.Description;

        var tooltip = $"{description}\n{startLabel} – {endLabel}\n{durationLabel}";
        return new TogglPhoneEntryViewModel(tooltip, dotTopOffset);
    }

    private static DateTime ToLocal(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Local => value,
            DateTimeKind.Utc => value.ToLocalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
        };
    }
}
