using CommunityToolkit.Mvvm.ComponentModel;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class TogglSleepCompactCardViewModel : ObservableObject
{
    private readonly ITogglSleepRepository _repository;
    private string _startLabel = "";
    private string _endLabel = "";
    private string _durationLabel = "";
    private string _countLabel = "";
    private bool _hasSingleEntry;
    private bool _hasMultipleEntries;
    private bool _hasNoEntries = true;

    public TogglSleepCompactCardViewModel(ITogglSleepRepository repository)
    {
        _repository = repository;
    }

    public string StartLabel
    {
        get => _startLabel;
        private set => SetProperty(ref _startLabel, value);
    }

    public string EndLabel
    {
        get => _endLabel;
        private set => SetProperty(ref _endLabel, value);
    }

    public string DurationLabel
    {
        get => _durationLabel;
        private set => SetProperty(ref _durationLabel, value);
    }

    public string CountLabel
    {
        get => _countLabel;
        private set => SetProperty(ref _countLabel, value);
    }

    public bool HasSingleEntry
    {
        get => _hasSingleEntry;
        private set
        {
            if (SetProperty(ref _hasSingleEntry, value))
            {
                OnPropertyChanged(nameof(SingleEntryVisibility));
            }
        }
    }

    public bool HasMultipleEntries
    {
        get => _hasMultipleEntries;
        private set
        {
            if (SetProperty(ref _hasMultipleEntries, value))
            {
                OnPropertyChanged(nameof(MultipleEntriesVisibility));
            }
        }
    }

    public bool HasNoEntries
    {
        get => _hasNoEntries;
        private set
        {
            if (SetProperty(ref _hasNoEntries, value))
            {
                OnPropertyChanged(nameof(NoEntriesVisibility));
            }
        }
    }

    public Visibility SingleEntryVisibility => HasSingleEntry ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MultipleEntriesVisibility => HasMultipleEntries ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoEntriesVisibility => HasNoEntries ? Visibility.Visible : Visibility.Collapsed;

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetSleepEntriesForDateAsync(date, ct);

        HasSingleEntry = entries.Count == 1;
        HasMultipleEntries = entries.Count > 1;
        HasNoEntries = entries.Count == 0;

        if (entries.Count == 1)
        {
            var entry = entries[0];
            var end = entry.EndTime ?? entry.StartTime;
            StartLabel = TogglSleepTimeFormatter.FormatTime(entry.StartTime);
            EndLabel = TogglSleepTimeFormatter.FormatEndTime(entry.StartTime, end);
            DurationLabel = TogglSleepTimeFormatter.FormatDuration(entry);
            CountLabel = "";
            return;
        }

        StartLabel = "";
        EndLabel = "";
        DurationLabel = "";
        CountLabel = entries.Count > 1 ? $"{entries.Count} sleep entries" : "";
    }
}
