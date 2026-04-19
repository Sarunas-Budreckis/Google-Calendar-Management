using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Models;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class CalendarQueryService : ICalendarQueryService
{
    private readonly IDbContextFactory<CalendarDbContext> _dbContextFactory;
    private readonly IColorMappingService _colorMappingService;
    private readonly ILogger<CalendarQueryService> _logger;

    public CalendarQueryService(
        IDbContextFactory<CalendarDbContext> dbContextFactory,
        IColorMappingService colorMappingService,
        ILogger<CalendarQueryService>? logger = null)
    {
        _dbContextFactory = dbContextFactory;
        _colorMappingService = colorMappingService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CalendarQueryService>.Instance;
    }

    public async Task<IList<CalendarEventDisplayModel>> GetEventsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var rangeStartUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var rangeEndExclusiveUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var rows = await (
            from gcalEvent in context.GcalEvents.AsNoTracking()
            join pendingEvent in context.PendingEvents.AsNoTracking()
                on gcalEvent.GcalEventId equals pendingEvent.GcalEventId into pendingEvents
            from pendingEvent in pendingEvents.DefaultIfEmpty()
            where !gcalEvent.IsDeleted &&
                  (
                      (gcalEvent.StartDatetime.HasValue &&
                       gcalEvent.StartDatetime.Value < rangeEndExclusiveUtc &&
                       (gcalEvent.EndDatetime ?? gcalEvent.StartDatetime.Value) >= rangeStartUtc) ||
                      (pendingEvent != null &&
                       pendingEvent.StartDatetime < rangeEndExclusiveUtc &&
                       pendingEvent.EndDatetime >= rangeStartUtc)
                  )
            orderby pendingEvent != null ? pendingEvent.StartDatetime : gcalEvent.StartDatetime,
                    pendingEvent != null ? pendingEvent.Summary : gcalEvent.Summary
            select new PendingCalendarQueryRow(gcalEvent, pendingEvent)
        ).ToListAsync(ct);

        var result = new List<CalendarEventDisplayModel>(rows.Count);
        foreach (var row in rows)
        {
            var model = TryMapToDisplayModel(row.GcalEvent, row.PendingEvent);
            if (model is not null && OverlapsRange(model, from, to))
            {
                result.Add(model);
            }
        }

        return result;
    }

    public async Task<CalendarEventDisplayModel?> GetEventByGcalIdAsync(string gcalEventId, CancellationToken ct = default)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
        var row = await (
            from gcalEvent in context.GcalEvents.AsNoTracking()
            join pendingEvent in context.PendingEvents.AsNoTracking()
                on gcalEvent.GcalEventId equals pendingEvent.GcalEventId into pendingEvents
            from pendingEvent in pendingEvents.DefaultIfEmpty()
            where !gcalEvent.IsDeleted && gcalEvent.GcalEventId == gcalEventId
            select new PendingCalendarQueryRow(gcalEvent, pendingEvent)
        ).SingleOrDefaultAsync(ct);

        return row is null ? null : TryMapToDisplayModel(row.GcalEvent, row.PendingEvent);
    }

    private CalendarEventDisplayModel? TryMapToDisplayModel(GcalEvent gcalEvent, PendingEvent? pendingEvent)
    {
        var effectiveStart = pendingEvent?.StartDatetime ?? gcalEvent.StartDatetime;
        if (effectiveStart is null)
        {
            _logger.LogWarning(
                "Skipping event {GcalEventId}: StartDatetime is null.",
                gcalEvent.GcalEventId);
            return null;
        }

        var effectiveEnd = pendingEvent?.EndDatetime ?? gcalEvent.EndDatetime ?? effectiveStart.Value;
        var effectiveTitle = pendingEvent?.Summary ?? gcalEvent.Summary ?? string.Empty;
        var effectiveDescription = pendingEvent?.Description ?? gcalEvent.Description;
        var effectiveColorId = pendingEvent?.ColorId ?? gcalEvent.ColorId;
        var isPending = pendingEvent is not null;

        var startUtc = NormalizeUtc(effectiveStart.Value);
        var endUtc = NormalizeUtc(effectiveEnd);
        var isAllDay = gcalEvent.IsAllDay ?? false;

        DateTime startLocal, endLocal;
        if (isAllDay)
        {
            // All-day events: the UTC date IS the calendar date — do not apply local timezone offset,
            // otherwise UTC− users see events shifted to the previous day.
            startLocal = startUtc.Date;
            endLocal = endUtc.Date;
        }
        else
        {
            startLocal = startUtc.ToLocalTime();
            endLocal = endUtc.ToLocalTime();
        }

        return new CalendarEventDisplayModel(
            gcalEvent.GcalEventId,
            effectiveTitle,
            startUtc,
            endUtc,
            startLocal,
            endLocal,
            isAllDay,
            _colorMappingService.GetHexColor(effectiveColorId),
            _colorMappingService.GetColorName(effectiveColorId),
            gcalEvent.IsRecurringInstance,
            effectiveDescription,
            gcalEvent.LastSyncedAt,
            isPending,
            isPending ? 0.6 : 1.0,
            pendingEvent?.UpdatedAt);
    }

    private static bool OverlapsRange(CalendarEventDisplayModel item, DateOnly from, DateOnly to)
    {
        var rangeStartLocal = from.ToDateTime(TimeOnly.MinValue);
        var rangeEndExclusiveLocal = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var effectiveEnd = item.EndLocal > item.StartLocal
            ? item.EndLocal
            : item.StartLocal;

        return item.StartLocal < rangeEndExclusiveLocal && effectiveEnd >= rangeStartLocal;
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

    private sealed record PendingCalendarQueryRow(GcalEvent GcalEvent, PendingEvent? PendingEvent);
}
