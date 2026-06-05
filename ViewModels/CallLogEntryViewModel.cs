using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.ViewModels;

public sealed class CallLogEntryViewModel
{
    private CallLogEntryViewModel() { }

    public string CallTypeLabel { get; private init; } = "";
    public string ContactLabel { get; private init; } = "";
    public string DurationLabel { get; private init; } = "";
    public string ServiceLabel { get; private init; } = "";
    public string TimeLabel { get; private init; } = "";

    public static CallLogEntryViewModel FromEntry(CallLogEntry entry)
    {
        var contact = !string.IsNullOrWhiteSpace(entry.Contact)
            ? entry.Contact
            : !string.IsNullOrWhiteSpace(entry.Number)
                ? entry.Number
                : "Unknown";

        return new CallLogEntryViewModel
        {
            CallTypeLabel = entry.CallType,
            ContactLabel = contact,
            DurationLabel = FormatDuration(entry.DurationSeconds),
            ServiceLabel = entry.Service,
            TimeLabel = entry.Date.ToLocalTime().ToString("HH:mm")
        };
    }

    public static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return "0s";
        }

        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
        }

        if (minutes > 0)
        {
            return seconds > 0 ? $"{minutes}m {seconds}s" : $"{minutes}m";
        }

        return $"{seconds}s";
    }
}
