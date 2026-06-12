using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class TogglSleepCompactCardViewModel : ObservableObject
{
    private readonly ITogglSleepRepository _repository;
    private readonly ITogglSleepQualityRepository _qualityRepository;
    private readonly IPendingEventDraftService? _pendingEventDraftService;
    private readonly ICalendarSelectionService? _calendarSelectionService;
    private string _startLabel = "";
    private string _endLabel = "";
    private string _durationLabel = "";
    private string _countLabel = "";
    private string _qualityLabel = "";
    private bool _hasSingleEntry;
    private bool _hasMultipleEntries;
    private bool _hasNoEntries = true;

    public TogglSleepCompactCardViewModel(
        ITogglSleepRepository repository,
        ITogglSleepQualityRepository qualityRepository,
        IPendingEventDraftService? pendingEventDraftService = null,
        ICalendarSelectionService? calendarSelectionService = null)
    {
        _repository = repository;
        _qualityRepository = qualityRepository;
        _pendingEventDraftService = pendingEventDraftService;
        _calendarSelectionService = calendarSelectionService;
    }

    public ObservableCollection<TogglSleepEntryViewModel> Entries { get; } = [];

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

    public string QualityLabel
    {
        get => _qualityLabel;
        private set
        {
            if (SetProperty(ref _qualityLabel, value))
            {
                OnPropertyChanged(nameof(QualityLabelVisibility));
            }
        }
    }

    public Visibility QualityLabelVisibility => !string.IsNullOrEmpty(_qualityLabel) ? Visibility.Visible : Visibility.Collapsed;

    public bool HasSingleEntry
    {
        get => _hasSingleEntry;
        private set
        {
            if (SetProperty(ref _hasSingleEntry, value))
            {
                OnPropertyChanged(nameof(SingleEntryVisibility));
                OnPropertyChanged(nameof(EntriesVisibility));
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
                OnPropertyChanged(nameof(EntriesVisibility));
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
    public Visibility EntriesVisibility => HasSingleEntry || HasMultipleEntries ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoEntriesVisibility => HasNoEntries ? Visibility.Visible : Visibility.Collapsed;

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetSleepEntriesForDateAsync(date, ct);
        Entries.Clear();
        Func<TogglEntry, Task>? addAction = HasAddSupport ? AddEntryAsync : null;
        foreach (var entry in entries)
        {
            Entries.Add(TogglSleepEntryViewModel.FromEntry(entry, addAction));
        }

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
        }
        else
        {
            StartLabel = "";
            EndLabel = "";
            DurationLabel = "";
            CountLabel = entries.Count > 1 ? $"{entries.Count} sleep entries" : "";
        }

        if (entries.Count == 0)
        {
            QualityLabel = "";
            return;
        }

        var quality = await _qualityRepository.GetQualityForDateAsync(date, ct);
        QualityLabel = quality.HasValue ? $"{quality}/10" : "";
    }

    private bool HasAddSupport =>
        _pendingEventDraftService is not null &&
        _calendarSelectionService is not null;

    private async Task AddEntryAsync(TogglEntry entry)
    {
        if (_pendingEventDraftService is null ||
            _calendarSelectionService is null)
        {
            return;
        }

        var startLocal = CalendarDraftTiming.RoundToNearestQuarterHour(
            NormalizeUtc(entry.StartTime).ToLocalTime());
        var endLocal = CalendarDraftTiming.RoundToNearestQuarterHour(
            NormalizeUtc(entry.EndTime ?? entry.StartTime).ToLocalTime());
        if (endLocal <= startLocal)
        {
            endLocal = startLocal.AddMinutes(15);
        }

        var candidate = await _pendingEventDraftService.CreateCandidateAsync(
            startLocal,
            endLocal,
            "Sleep",
            sourceSystem: "toggl",
            colorId: "grey");
        _calendarSelectionService.Select(candidate.EventId, CalendarEventSourceKind.Candidate, openInEditMode: true);
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
