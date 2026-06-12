using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class TogglTransitDrilldownViewModel : ObservableObject
{
    private readonly ITogglTransitRepository _repository;
    private readonly IPendingEventDraftService _pendingEventDraftService;
    private readonly ICalendarSelectionService _calendarSelectionService;
    private readonly EightFifteenRuleService _eightFifteenRule;
    private readonly List<TogglEntry> _entries = [];
    private bool _hasEntries;

    public TogglTransitDrilldownViewModel(
        ITogglTransitRepository repository,
        IPendingEventDraftService pendingEventDraftService,
        ICalendarSelectionService calendarSelectionService,
        EightFifteenRuleService eightFifteenRule)
    {
        _repository = repository;
        _pendingEventDraftService = pendingEventDraftService;
        _calendarSelectionService = calendarSelectionService;
        _eightFifteenRule = eightFifteenRule;
        CreateCandidateEventsCommand = new AsyncRelayCommand(CreateCandidateEventsAsync, () => HasEntries);
    }

    public ObservableCollection<TogglTransitSessionViewModel> Sessions { get; } = [];

    public bool HasEntries
    {
        get => _hasEntries;
        private set
        {
            if (SetProperty(ref _hasEntries, value))
            {
                OnPropertyChanged(nameof(SessionsVisibility));
                OnPropertyChanged(nameof(EmptyStateVisibility));
                CreateCandidateEventsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public Visibility SessionsVisibility => HasEntries ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyStateVisibility => HasEntries ? Visibility.Collapsed : Visibility.Visible;

    public IAsyncRelayCommand CreateCandidateEventsCommand { get; }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetTransitEntriesForDateAsync(date, ct);
        _entries.Clear();
        _entries.AddRange(entries);

        Sessions.Clear();
        foreach (var entry in entries)
        {
            Sessions.Add(TogglTransitSessionViewModel.FromEntry(entry));
        }

        HasEntries = Sessions.Count > 0;
    }

    private async Task CreateCandidateEventsAsync()
    {
        if (_entries.Count == 0)
        {
            return;
        }

        var createdEvents = new List<Event>();

        foreach (var entry in _entries)
        {
            var startUtc = NormalizeUtc(entry.StartTime);
            var endUtc = NormalizeUtc(entry.EndTime ?? entry.StartTime);
            var startLocal = startUtc.ToLocalTime();
            var endLocal = endUtc.ToLocalTime();

            var blocks = _eightFifteenRule.ApplyRule(startLocal, endLocal);
            foreach (var (blockStart, blockEnd) in blocks)
            {
                var candidate = await _pendingEventDraftService.CreateCandidateAsync(
                    blockStart,
                    blockEnd,
                    "Driving",
                    sourceSystem: "toggl",
                    colorId: "lavender");
                createdEvents.Add(candidate);
            }
        }

        if (createdEvents.Count == 0)
        {
            return;
        }

        var selectId = createdEvents[0].EventId;
        _calendarSelectionService.Select(selectId, CalendarEventSourceKind.Candidate, openInEditMode: true);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
