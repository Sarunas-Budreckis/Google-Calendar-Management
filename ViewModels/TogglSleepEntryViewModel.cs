using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Data.Entities;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class TogglSleepEntryViewModel
{
    public TogglSleepEntryViewModel(
        string startLabel,
        string endLabel,
        string timeRangeLabel,
        string durationLabel,
        string description,
        Func<Task>? addAction = null)
    {
        StartLabel = startLabel;
        EndLabel = endLabel;
        TimeRangeLabel = timeRangeLabel;
        DurationLabel = durationLabel;
        Description = description;
        AddCommand = new AsyncRelayCommand(
            async () =>
            {
                if (addAction is not null)
                {
                    await addAction();
                }
            },
            () => addAction is not null);
    }

    public string StartLabel { get; }
    public string EndLabel { get; }
    public string TimeRangeLabel { get; }
    public string DurationLabel { get; }
    public string Description { get; }
    public IAsyncRelayCommand AddCommand { get; }
    public Visibility AddButtonVisibility => AddCommand.CanExecute(null) ? Visibility.Visible : Visibility.Collapsed;

    public static TogglSleepEntryViewModel FromEntry(TogglEntry entry, Func<TogglEntry, Task>? addAction = null)
    {
        var end = entry.EndTime ?? entry.StartTime;
        return new TogglSleepEntryViewModel(
            TogglSleepTimeFormatter.FormatTime(entry.StartTime),
            TogglSleepTimeFormatter.FormatEndTime(entry.StartTime, end),
            TogglSleepTimeFormatter.FormatTimeRange(entry.StartTime, end),
            TogglSleepTimeFormatter.FormatDuration(entry),
            string.IsNullOrWhiteSpace(entry.Description) ? "(No description)" : entry.Description,
            addAction is null ? null : () => addAction(entry));
    }
}
