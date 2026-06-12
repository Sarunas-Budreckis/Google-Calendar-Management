using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Views;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Services;

public sealed class TogglSleepCardProvider : IDataSourceCardProvider, IDataSourceCardProviderPreloader, IDataSourceViewDataProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITogglSleepRepository _repository;
    private readonly IPendingEventDraftService _pendingEventDraftService;
    private readonly IPendingEventRepository _pendingEventRepository;
    private readonly IEventRepository? _eventRepository;
    private readonly ICalendarSelectionService _calendarSelectionService;
    private readonly ConcurrentDictionary<DateOnly, bool> _hasDataByDate = [];

    public TogglSleepCardProvider(
        IServiceProvider serviceProvider,
        ITogglSleepRepository repository,
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

    public string SourceKey => TogglSleepImportService.SourceKey;

    public async Task PreloadAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetSleepEntriesForDateAsync(date, ct);
        _hasDataByDate[date] = entries.Count > 0;
    }

    public bool? HasDataForDay(DateOnly date)
    {
        return _hasDataByDate.TryGetValue(date, out var hasData) ? hasData : null;
    }

    public async Task<IReadOnlyList<DataSourceDayData>> GetDataForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var counts = await _repository.GetSleepEntryCountsForRangeAsync(from, to, ct);
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
        var control = _serviceProvider.GetRequiredService<TogglSleepCompactCardControl>();
        _ = control.LoadAsync(date);
        return control;
    }

    public UIElement CreateDrilldownView(DateOnly date)
    {
        var control = _serviceProvider.GetRequiredService<TogglSleepDrilldownControl>();
        _ = control.LoadAsync(date);
        return control;
    }

    public async Task AddForDayAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetSleepEntriesForDateAsync(date, ct);
        if (entries.Count == 0)
        {
            return;
        }

        var startLocal = CalendarDraftTiming.RoundToNearestQuarterHour(
            entries.Min(entry => NormalizeUtc(entry.StartTime)).ToLocalTime());
        var endLocal = CalendarDraftTiming.RoundToNearestQuarterHour(
            entries.Max(entry => NormalizeUtc(entry.EndTime ?? entry.StartTime)).ToLocalTime());
        if (endLocal <= startLocal)
        {
            endLocal = startLocal.AddMinutes(15);
        }

        var draft = await _pendingEventDraftService.CreateDraftAsync(startLocal, endLocal, "Sleep", ct);
        draft.Summary = "Sleep";
        draft.IsAllDay = false;
        draft.SourceSystem = "toggl";
        draft.ColorId = "grey";
        if (_eventRepository is not null)
        {
            await _eventRepository.UpsertAsync(draft, ct);
        }
        WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(draft.EventId));
        _calendarSelectionService.Select(draft.EventId, CalendarEventSourceKind.Pending, openInEditMode: true);
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
