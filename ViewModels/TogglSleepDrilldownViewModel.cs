using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;
using System.Text.RegularExpressions;

namespace GoogleCalendarManagement.ViewModels;

public sealed partial class TogglSleepDrilldownViewModel : ObservableObject
{
    private readonly ITogglSleepRepository _repository;
    private readonly ITogglSleepQualityRepository _qualityRepository;
    private readonly IPendingEventDraftService _pendingEventDraftService;
    private readonly IEventRepository _eventRepository;
    private readonly ICalendarSelectionService _calendarSelectionService;
    private readonly List<TogglEntry> _entries = [];
    private bool _hasEntries;
    private int? _quality;
    private DateOnly _currentDate;

    public TogglSleepDrilldownViewModel(
        ITogglSleepRepository repository,
        ITogglSleepQualityRepository qualityRepository,
        IPendingEventDraftService pendingEventDraftService,
        ICalendarSelectionService calendarSelectionService,
        IEventRepository eventRepository)
    {
        _repository = repository;
        _qualityRepository = qualityRepository;
        _pendingEventDraftService = pendingEventDraftService;
        _eventRepository = eventRepository;
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
                OnPropertyChanged(nameof(QualityInputVisibility));
                CreateCandidateEventCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int? Quality
    {
        get => _quality;
        private set => SetProperty(ref _quality, value);
    }

    public Visibility EntriesVisibility => HasEntries ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyStateVisibility => HasEntries ? Visibility.Collapsed : Visibility.Visible;
    public Visibility QualityInputVisibility => HasEntries ? Visibility.Visible : Visibility.Collapsed;

    public IAsyncRelayCommand CreateCandidateEventCommand { get; }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        _currentDate = date;
        var entries = await _repository.GetSleepEntriesForDateAsync(date, ct);
        _entries.Clear();
        _entries.AddRange(entries);

        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(TogglSleepEntryViewModel.FromEntry(entry, AddEntryAsync));
        }

        HasEntries = Entries.Count > 0;
        Quality = HasEntries ? await _qualityRepository.GetQualityForDateAsync(date, ct) : null;
    }

    public async Task SetQualityAsync(int? quality, CancellationToken ct = default)
    {
        if (!HasEntries)
        {
            return;
        }

        if (quality is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(quality), quality, "Sleep quality must be between 0 and 10.");
        }

        await _qualityRepository.UpsertQualityAsync(_currentDate, quality, ct);
        Quality = quality;
        await UpdateSleepEventTitleAsync(quality, ct);
    }

    private async Task UpdateSleepEventTitleAsync(int? quality, CancellationToken ct)
    {
        var sleepEvent = await _eventRepository.GetSleepEventForDateAsync(_currentDate, ct);
        if (sleepEvent is not null)
        {
            MarkEventTitleChanged(sleepEvent, quality);
            await _eventRepository.UpsertAsync(sleepEvent, ct);
            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(sleepEvent.EventId));
            return;
        }

        var linkedGcalEventId = _entries
            .Where(e => e.LinkedEventType == "gcal" && !string.IsNullOrEmpty(e.LinkedEventId))
            .Select(e => e.LinkedEventId!)
            .FirstOrDefault();

        if (linkedGcalEventId is null)
        {
            return;
        }

        var linkedEvent = await _eventRepository.GetByGcalEventIdAsync(linkedGcalEventId, ct);
        if (linkedEvent is null)
        {
            return;
        }

        MarkEventTitleChanged(linkedEvent, quality);
        await _eventRepository.UpsertAsync(linkedEvent, ct);
        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(linkedEvent.EventId));
    }

    private static void MarkEventTitleChanged(Event ev, int? quality)
    {
        ev.Summary = BuildSleepTitle(ev.Summary, quality);
        if (ev.Publish == "published")
        {
            ev.HasUnpublishedChanges = true;
        }

        ev.UpdatedAt = DateTime.UtcNow;
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

        var candidate = await _pendingEventDraftService.CreateCandidateAsync(
            startLocal,
            endLocal,
            BuildSleepTitle(_quality),
            sourceSystem: "toggl",
            colorId: "grey");
        _calendarSelectionService.Select(candidate.EventId, CalendarEventSourceKind.Candidate, openInEditMode: true);
    }

    private async Task AddEntryAsync(TogglEntry entry)
    {
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
            BuildSleepTitle(_quality),
            sourceSystem: "toggl",
            colorId: "grey");
        _calendarSelectionService.Select(candidate.EventId, CalendarEventSourceKind.Candidate, openInEditMode: true);
    }

    private static string BuildSleepTitle(string? currentTitle, int? quality)
    {
        var baseTitle = StripQualitySuffix(currentTitle);
        return quality.HasValue ? $"{baseTitle} – {quality}/10" : baseTitle;
    }

    private static string BuildSleepTitle(int? quality)
        => BuildSleepTitle("Sleep", quality);

    private static string StripQualitySuffix(string? title)
    {
        var baseTitle = string.IsNullOrWhiteSpace(title) ? "Sleep" : title.Trim();
        return SleepQualitySuffixRegex().Replace(baseTitle, "");
    }

    [GeneratedRegex(@"\s+–\s+\d{1,2}/10$")]
    private static partial Regex SleepQualitySuffixRegex();

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
