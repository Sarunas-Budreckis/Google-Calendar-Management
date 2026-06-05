using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.Messaging;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Services;

public sealed class TogglTransitCardProvider : IDataSourceCardProvider, IDataSourceCardProviderPreloader, IDataSourceViewDataProvider, IDataSourceDayActionProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITogglTransitRepository _repository;
    private readonly IPendingEventDraftService _pendingEventDraftService;
    private readonly IPendingEventRepository _pendingEventRepository;
    private readonly ICalendarSelectionService _calendarSelectionService;
    private readonly EightFifteenRuleService _eightFifteenRule;
    private readonly ConcurrentDictionary<DateOnly, bool> _hasDataByDate = [];

    public TogglTransitCardProvider(
        IServiceProvider serviceProvider,
        ITogglTransitRepository repository,
        IPendingEventDraftService pendingEventDraftService,
        IPendingEventRepository pendingEventRepository,
        ICalendarSelectionService calendarSelectionService,
        EightFifteenRuleService eightFifteenRule)
    {
        _serviceProvider = serviceProvider;
        _repository = repository;
        _pendingEventDraftService = pendingEventDraftService;
        _pendingEventRepository = pendingEventRepository;
        _calendarSelectionService = calendarSelectionService;
        _eightFifteenRule = eightFifteenRule;
    }

    public string SourceKey => TogglTransitImportService.SourceKey;

    public async Task PreloadAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetTransitEntriesForDateAsync(date, ct);
        _hasDataByDate[date] = entries.Count > 0;
    }

    public bool? HasDataForDay(DateOnly date)
    {
        return _hasDataByDate.TryGetValue(date, out var hasData) ? hasData : null;
    }

    public async Task<IReadOnlyList<DataSourceDayData>> GetDataForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var counts = await _repository.GetTransitEntryCountsForRangeAsync(from, to, ct);
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
        var control = _serviceProvider.GetRequiredService<TogglTransitCompactCardControl>();
        _ = control.LoadAsync(date);
        return control;
    }

    public UIElement CreateDrilldownView(DateOnly date)
    {
        var control = _serviceProvider.GetRequiredService<TogglTransitDrilldownControl>();
        _ = control.LoadAsync(date);
        return control;
    }

    public async Task AddForDayAsync(DateOnly date, CancellationToken ct = default)
    {
        var entries = await _repository.GetTransitEntriesForDateAsync(date, ct);
        if (entries.Count == 0)
        {
            return;
        }

        var createdEvents = new List<PendingEvent>();

        foreach (var entry in entries)
        {
            var startUtc = NormalizeUtc(entry.StartTime);
            var endUtc = NormalizeUtc(entry.EndTime ?? entry.StartTime);
            var startLocal = startUtc.ToLocalTime();
            var endLocal = endUtc.ToLocalTime();

            var blocks = _eightFifteenRule.ApplyRule(startLocal, endLocal);
            foreach (var (blockStart, blockEnd) in blocks)
            {
                var draft = await _pendingEventDraftService.CreateDraftAsync(blockStart, blockEnd, "Driving", ct);
                draft.Summary = "Driving";
                draft.IsAllDay = false;
                draft.SourceSystem = "toggl";
                draft.ColorId = "lavender";
                await _pendingEventRepository.UpsertAsync(draft, ct);
                WeakReferenceMessenger.Default.Send(new EventUpdatedMessage(draft.PendingEventId));
                createdEvents.Add(draft);
            }
        }

        if (createdEvents.Count == 0)
        {
            return;
        }

        var selectId = createdEvents[0].PendingEventId;
        _calendarSelectionService.Select(selectId, CalendarEventSourceKind.Pending, openInEditMode: true);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
