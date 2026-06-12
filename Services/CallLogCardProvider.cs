using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Services;

public sealed class CallLogCardProvider : IDataSourceCardProvider, IDataSourceCardProviderPreloader, IDataSourceViewDataProvider, IDataSourceDayActionProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICallLogRepository _repository;
    private readonly IPendingEventDraftService _pendingEventDraftService;
    private readonly IPendingEventRepository _pendingEventRepository;
    private readonly IEventRepository? _eventRepository;
    private readonly ICalendarSelectionService _calendarSelectionService;
    private readonly ConcurrentDictionary<DateOnly, bool> _hasDataByDate = [];

    public CallLogCardProvider(
        IServiceProvider serviceProvider,
        ICallLogRepository repository,
        IPendingEventDraftService pendingEventDraftService,
        IPendingEventRepository pendingEventRepository,
        ICalendarSelectionService calendarSelectionService,
        IEventRepository? eventRepository = null)
    {
        _serviceProvider = serviceProvider;
        _repository = repository;
        _pendingEventDraftService = pendingEventDraftService;
        _pendingEventRepository = pendingEventRepository;
        _eventRepository = eventRepository;
        _calendarSelectionService = calendarSelectionService;
    }

    public string SourceKey => CallLogImportService.SourceKey;

    public async Task PreloadAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetEntriesForDateAsync(date, ct);
        _hasDataByDate[date] = entries.Count > 0;
    }

    public bool? HasDataForDay(DateOnly date)
    {
        return _hasDataByDate.TryGetValue(date, out var hasData) ? hasData : null;
    }

    public async Task<IReadOnlyList<DataSourceDayData>> GetDataForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var counts = await _repository.GetEntryCountsForRangeAsync(from, to, ct);
        var result = new List<DataSourceDayData>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            counts.TryGetValue(date, out var count);
            result.Add(new DataSourceDayData(date, count > 0, count > 0 ? count : null));
        }

        return result;
    }

    public UIElement? CreateCompactSummaryView(DateOnly date)
    {
        var control = _serviceProvider.GetRequiredService<CallLogCompactCardControl>();
        _ = control.LoadAsync(date);
        return control;
    }

    public UIElement CreateDrilldownView(DateOnly date)
    {
        var control = _serviceProvider.GetRequiredService<CallLogDrilldownControl>();
        _ = control.LoadAsync(date);
        return control;
    }

    public async Task AddForDayAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetEntriesForDateAsync(date, ct);
        var qualifying = entries.Where(e => e.DurationSeconds >= 600).ToList();
        if (qualifying.Count == 0)
        {
            return;
        }

        string? lastPendingEventId = null;
        foreach (var entry in qualifying)
        {
            var startLocal = DateTime.SpecifyKind(entry.Date, DateTimeKind.Local);
            var endLocal = startLocal.AddSeconds(entry.DurationSeconds);
            var draft = await _pendingEventDraftService.CreateDraftAsync(startLocal, endLocal, BuildTitle(entry), ct);
            draft.Summary = BuildTitle(entry);
            draft.IsAllDay = false;
            draft.SourceSystem = "call_log";
            draft.ColorId = "azure";
            if (_eventRepository is not null)
            {
                await _eventRepository.UpsertAsync(draft, ct);
            }
            WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(draft.EventId));
            lastPendingEventId = draft.EventId;
        }

        if (lastPendingEventId is not null)
        {
            _calendarSelectionService.Select(lastPendingEventId, CalendarEventSourceKind.Pending, openInEditMode: true);
        }
    }

    private static string BuildTitle(Data.Entities.CallLogEntry entry)
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
