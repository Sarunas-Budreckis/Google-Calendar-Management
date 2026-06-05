using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.ViewModels;

public sealed class TogglTransitSessionViewModel
{
    public TogglTransitSessionViewModel(string startLabel, string endLabel, string durationLabel)
    {
        StartLabel = startLabel;
        EndLabel = endLabel;
        DurationLabel = durationLabel;
    }

    public string StartLabel { get; }
    public string EndLabel { get; }
    public string DurationLabel { get; }

    public static TogglTransitSessionViewModel FromEntry(TogglEntry entry)
    {
        var end = entry.EndTime ?? entry.StartTime;
        return new TogglTransitSessionViewModel(
            TogglSleepTimeFormatter.FormatTime(entry.StartTime),
            TogglSleepTimeFormatter.FormatEndTime(entry.StartTime, end),
            TogglSleepTimeFormatter.FormatDuration(entry));
    }
}
