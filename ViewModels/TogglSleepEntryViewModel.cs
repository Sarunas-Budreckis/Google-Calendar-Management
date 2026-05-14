using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.ViewModels;

public sealed class TogglSleepEntryViewModel
{
    public TogglSleepEntryViewModel(string startLabel, string endLabel, string durationLabel, string description)
    {
        StartLabel = startLabel;
        EndLabel = endLabel;
        DurationLabel = durationLabel;
        Description = description;
    }

    public string StartLabel { get; }
    public string EndLabel { get; }
    public string DurationLabel { get; }
    public string Description { get; }

    public static TogglSleepEntryViewModel FromEntry(TogglEntry entry)
    {
        var end = entry.EndTime ?? entry.StartTime;
        return new TogglSleepEntryViewModel(
            TogglSleepTimeFormatter.FormatTime(entry.StartTime),
            TogglSleepTimeFormatter.FormatEndTime(entry.StartTime, end),
            TogglSleepTimeFormatter.FormatDuration(entry),
            string.IsNullOrWhiteSpace(entry.Description) ? "(No description)" : entry.Description);
    }
}
