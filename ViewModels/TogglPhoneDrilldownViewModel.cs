using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class TogglPhoneDrilldownViewModel : ObservableObject
{
    private const double CanvasHeight = 480.0;

    private readonly ITogglPhoneRepository _repository;
    private readonly IPendingEventDraftService _pendingEventDraftService;
    private readonly ICalendarSelectionService _calendarSelectionService;
    private readonly TogglSlidingWindowService _slidingWindowService;

    private readonly List<TogglEntry> _entries = [];
    private bool _hasEntries;

    public TogglPhoneDrilldownViewModel(
        ITogglPhoneRepository repository,
        IPendingEventDraftService pendingEventDraftService,
        ICalendarSelectionService calendarSelectionService,
        TogglSlidingWindowService slidingWindowService)
    {
        _repository = repository;
        _pendingEventDraftService = pendingEventDraftService;
        _calendarSelectionService = calendarSelectionService;
        _slidingWindowService = slidingWindowService;

        CreateCandidateEventsCommand = new AsyncRelayCommand(CreateCandidateEventsAsync, () => HasEntries);
    }

    public ObservableCollection<TogglPhoneEntryViewModel> TimelineDots { get; } = [];

    public bool HasEntries
    {
        get => _hasEntries;
        private set
        {
            if (SetProperty(ref _hasEntries, value))
            {
                OnPropertyChanged(nameof(TimelineVisibility));
                OnPropertyChanged(nameof(EmptyStateVisibility));
                CreateCandidateEventsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public Visibility TimelineVisibility => HasEntries ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyStateVisibility => HasEntries ? Visibility.Collapsed : Visibility.Visible;

    public IAsyncRelayCommand CreateCandidateEventsCommand { get; }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetPhoneEntriesForDateAsync(date, ct);
        _entries.Clear();
        _entries.AddRange(entries);

        TimelineDots.Clear();
        foreach (var entry in entries)
        {
            TimelineDots.Add(TogglPhoneEntryViewModel.FromEntry(entry, CanvasHeight));
        }

        HasEntries = TimelineDots.Count > 0;
    }

    private async Task CreateCandidateEventsAsync()
    {
        if (_entries.Count == 0)
        {
            return;
        }

        var windowEntries = _entries
            .Where(e => e.EndTime.HasValue)
            .Select(e => new TogglSlidingWindowService.SlidingWindowEntry(
                NormalizeUtc(e.StartTime),
                NormalizeUtc(e.EndTime!.Value)))
            .ToList();

        if (windowEntries.Count == 0)
        {
            return;
        }

        var windows = _slidingWindowService.ComputeWindows(
            windowEntries,
            gapThreshold: TimeSpan.FromMinutes(15),
            qualityThreshold: 0.5,
            minWindowDuration: TimeSpan.FromMinutes(5));

        string? firstEventId = null;
        foreach (var window in windows)
        {
            var startLocal = CalendarDraftTiming.RoundToNearestQuarterHour(window.WindowStartUtc.ToLocalTime());
            var endLocal = CalendarDraftTiming.RoundToNearestQuarterHour(window.WindowEndUtc.ToLocalTime());
            if (endLocal <= startLocal)
            {
                endLocal = startLocal.AddMinutes(15);
            }

            var candidate = await _pendingEventDraftService.CreateCandidateAsync(
                startLocal,
                endLocal,
                "Phone",
                sourceSystem: "toggl",
                colorId: "banana");

            firstEventId ??= candidate.EventId;
        }

        if (firstEventId is not null)
        {
            _calendarSelectionService.Select(firstEventId, CalendarEventSourceKind.Candidate, openInEditMode: true);
        }
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
