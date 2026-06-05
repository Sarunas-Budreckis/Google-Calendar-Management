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

public sealed class CallLogDrilldownViewModel : ObservableObject
{
    private readonly ICallLogRepository _repository;
    private readonly IPendingEventDraftService _pendingEventDraftService;
    private readonly IPendingEventRepository _pendingEventRepository;
    private readonly ICalendarSelectionService _calendarSelectionService;
    private readonly List<CallLogEntry> _entries = [];
    private bool _hasEntries;
    private DateOnly _currentDate;

    public CallLogDrilldownViewModel(
        ICallLogRepository repository,
        IPendingEventDraftService pendingEventDraftService,
        IPendingEventRepository pendingEventRepository,
        ICalendarSelectionService calendarSelectionService)
    {
        _repository = repository;
        _pendingEventDraftService = pendingEventDraftService;
        _pendingEventRepository = pendingEventRepository;
        _calendarSelectionService = calendarSelectionService;
        CreateCandidateEventsCommand = new AsyncRelayCommand(CreateCandidateEventsAsync, () => HasEntries);
    }

    public ObservableCollection<CallLogEntryViewModel> Entries { get; } = [];

    public bool HasEntries
    {
        get => _hasEntries;
        private set
        {
            if (SetProperty(ref _hasEntries, value))
            {
                OnPropertyChanged(nameof(EntriesVisibility));
                OnPropertyChanged(nameof(EmptyStateVisibility));
                CreateCandidateEventsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public Visibility EntriesVisibility => HasEntries ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyStateVisibility => HasEntries ? Visibility.Collapsed : Visibility.Visible;

    public IAsyncRelayCommand CreateCandidateEventsCommand { get; }

    public async Task LoadAsync(DateOnly date, CancellationToken ct = default)
    {
        _currentDate = date;
        var entries = await _repository.GetEntriesForDateAsync(date, ct);
        _entries.Clear();
        _entries.AddRange(entries);

        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(CallLogEntryViewModel.FromEntry(entry));
        }

        HasEntries = Entries.Count > 0;
    }

    private async Task CreateCandidateEventsAsync()
    {
        var qualifying = _entries.Where(e => e.DurationSeconds >= 600).ToList();
        if (qualifying.Count == 0)
        {
            return;
        }

        string? lastPendingEventId = null;
        foreach (var entry in qualifying)
        {
            var startLocal = DateTime.SpecifyKind(entry.Date, DateTimeKind.Local);
            var endLocal = startLocal.AddSeconds(entry.DurationSeconds);
            var draft = await _pendingEventDraftService.CreateDraftAsync(startLocal, endLocal, BuildTitle(entry));
            draft.Summary = BuildTitle(entry);
            draft.IsAllDay = false;
            draft.SourceSystem = "call_log";
            draft.ColorId = "azure";
            await _pendingEventRepository.UpsertAsync(draft);
            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(draft.PendingEventId));
            lastPendingEventId = draft.PendingEventId;
        }

        if (lastPendingEventId is not null)
        {
            _calendarSelectionService.Select(lastPendingEventId, CalendarEventSourceKind.Pending, openInEditMode: true);
        }
    }

    private static string BuildTitle(CallLogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Contact))
        {
            return entry.Contact;
        }

        if (!string.IsNullOrWhiteSpace(entry.Number))
        {
            return entry.Number;
        }

        return "Phone Call";
    }
}
