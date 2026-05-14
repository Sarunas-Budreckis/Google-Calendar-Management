using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class TogglSleepDrilldownViewModel : ObservableObject
{
    private readonly ITogglSleepRepository _repository;
    private readonly IPendingEventDraftService _pendingEventDraftService;
    private readonly IPendingEventRepository _pendingEventRepository;
    private readonly ICalendarSelectionService _calendarSelectionService;
    private readonly List<TogglEntry> _entries = [];
    private bool _hasEntries;

    public TogglSleepDrilldownViewModel(
        ITogglSleepRepository repository,
        IPendingEventDraftService pendingEventDraftService,
        IPendingEventRepository pendingEventRepository,
        ICalendarSelectionService calendarSelectionService)
    {
        _repository = repository;
        _pendingEventDraftService = pendingEventDraftService;
        _pendingEventRepository = pendingEventRepository;
        _calendarSelectionService = calendarSelectionService;
        CreateCandidateEventCommand = new AsyncRelayCommand(CreateCandidateEventAsync, () => HasEntries);
    }

    public ObservableCollection<TogglSleepEntryViewModel> Entries { get; } = [];

    public bool HasEntries
    {
        get => _hasEntries;
        private set
        {
            if (SetProperty(ref _hasEntries, value))
            {
                OnPropertyChanged(nameof(EntriesVisibility));
                OnPropertyChanged(nameof(EmptyStateVisibility));
                CreateCandidateEventCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public Visibility EntriesVisibility => HasEntries ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyStateVisibility => HasEntries ? Visibility.Collapsed : Visibility.Visible;

    public IAsyncRelayCommand CreateCandidateEventCommand { get; }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetSleepEntriesForDateAsync(date, ct);
        _entries.Clear();
        _entries.AddRange(entries);

        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(TogglSleepEntryViewModel.FromEntry(entry));
        }

        HasEntries = Entries.Count > 0;
    }

    private async Task CreateCandidateEventAsync()
    {
        if (_entries.Count == 0)
        {
            return;
        }

        var startLocal = CalendarDraftTiming.RoundToNearestQuarterHour(
            _entries.Min(entry => NormalizeUtc(entry.StartTime)).ToLocalTime());
        var endLocal = CalendarDraftTiming.RoundToNearestQuarterHour(
            _entries.Max(entry => NormalizeUtc(entry.EndTime ?? entry.StartTime)).ToLocalTime());
        if (endLocal <= startLocal)
        {
            endLocal = startLocal.AddMinutes(15);
        }

        var draft = await _pendingEventDraftService.CreateDraftAsync(startLocal, endLocal, "Sleep");
        draft.Summary = "Sleep";
        draft.IsAllDay = false;
        draft.SourceSystem = "toggl";
        draft.ColorId = "grey";
        await _pendingEventRepository.UpsertAsync(draft);
        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(draft.PendingEventId));
        _calendarSelectionService.Select(draft.PendingEventId, CalendarEventSourceKind.Pending, openInEditMode: true);
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
