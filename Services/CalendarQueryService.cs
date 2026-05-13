using GoogleCalendarManagement.Data;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Models;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace GoogleCalendarManagement.Services;

public sealed class CalendarQueryService : ICalendarQueryService
{
    private const string DraftStatusLabel = "Not yet published to Google Calendar";
    private const string PendingOverlayStatusLabel = "Local changes, pending push to GCal";
    private const string PendingDeleteStatusLabel = "Pending delete — will be removed from Google Calendar when pushed";

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
        var googleRows = await (
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
                       pendingEvent.StartDatetime.HasValue &&
                       pendingEvent.StartDatetime.Value < rangeEndExclusiveUtc &&
                       (pendingEvent.EndDatetime ?? pendingEvent.StartDatetime.Value) >= rangeStartUtc)
                  )
            orderby pendingEvent != null ? pendingEvent.StartDatetime : gcalEvent.StartDatetime,
                    pendingEvent != null ? pendingEvent.Summary : gcalEvent.Summary
            select new GoogleCalendarQueryRow(gcalEvent, pendingEvent)
        ).ToListAsync(ct);

        var draftRows = await context.PendingEvents
            .AsNoTracking()
            .Where(pendingEvent =>
                pendingEvent.GcalEventId == null &&
                pendingEvent.StartDatetime.HasValue &&
                pendingEvent.StartDatetime.Value < rangeEndExclusiveUtc &&
                (pendingEvent.EndDatetime ?? pendingEvent.StartDatetime.Value) >= rangeStartUtc)
            .OrderBy(pendingEvent => pendingEvent.StartDatetime)
            .ThenBy(pendingEvent => pendingEvent.Summary)
            .ToListAsync(ct);

        var result = new List<CalendarEventDisplayModel>(googleRows.Count + draftRows.Count);
        foreach (var row in googleRows)
        {
            var model = TryMapGoogleEventToDisplayModel(row.GcalEvent, row.PendingEvent);
            if (model is not null && OverlapsRange(model, from, to))
            {
                result.Add(model);
            }
        }

        foreach (var pendingEvent in draftRows)
        {
            var model = TryMapPendingDraftToDisplayModel(pendingEvent);
            if (model is not null && OverlapsRange(model, from, to))
            {
                result.Add(model);
            }
        }

        result.Sort(static (left, right) =>
        {
            var startComparison = left.StartLocal.CompareTo(right.StartLocal);
            if (startComparison != 0)
            {
                return startComparison;
            }

            var endComparison = left.EndLocal.CompareTo(right.EndLocal);
            if (endComparison != 0)
            {
                return endComparison;
            }

            return StringComparer.CurrentCulture.Compare(left.Title, right.Title);
        });

        return result;
    }

    public async Task<CalendarEventDisplayModel?> GetEventByIdAsync(string eventId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);

        if (eventId.StartsWith("pending_", StringComparison.Ordinal))
        {
            var pendingDraft = await context.PendingEvents
                .AsNoTracking()
                .SingleOrDefaultAsync(pendingEvent => pendingEvent.PendingEventId == eventId, ct);
            return pendingDraft is null
                ? null
                : pendingDraft.GcalEventId is null
                    ? TryMapPendingDraftToDisplayModel(pendingDraft)
                    : await GetGoogleEventByIdAsync(context, pendingDraft.GcalEventId, ct);
        }

        return await GetGoogleEventByIdAsync(context, eventId, ct);
    }

    private async Task<CalendarEventDisplayModel?> GetGoogleEventByIdAsync(CalendarDbContext context, string gcalEventId, CancellationToken ct)
    {
        var row = await (
            from gcalEvent in context.GcalEvents.AsNoTracking()
            join pendingEvent in context.PendingEvents.AsNoTracking()
                on gcalEvent.GcalEventId equals pendingEvent.GcalEventId into pendingEvents
            from pendingEvent in pendingEvents.DefaultIfEmpty()
            where !gcalEvent.IsDeleted && gcalEvent.GcalEventId == gcalEventId
            select new GoogleCalendarQueryRow(gcalEvent, pendingEvent)
        ).SingleOrDefaultAsync(ct);

        return row is null ? null : TryMapGoogleEventToDisplayModel(row.GcalEvent, row.PendingEvent);
    }

    private CalendarEventDisplayModel? TryMapGoogleEventToDisplayModel(GcalEvent gcalEvent, PendingEvent? pendingEvent)
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
        var effectiveColorKey = _colorMappingService.NormalizeColorKey(effectiveColorId);
        var isPending = pendingEvent is not null;
        var isPendingDelete = pendingEvent?.OperationType == "delete";

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
            CalendarEventSourceKind.Google,
            effectiveTitle,
            startUtc,
            endUtc,
            startLocal,
            endLocal,
            isAllDay,
            _colorMappingService.GetHexColor(effectiveColorKey),
            _colorMappingService.GetDisplayName(effectiveColorKey),
            gcalEvent.IsRecurringInstance,
            effectiveDescription,
            gcalEvent.LastSyncedAt,
            isPending,
            false,
            isPending ? 0.6 : 1.0,
            pendingEvent?.UpdatedAt,
            isPendingDelete ? PendingDeleteStatusLabel : isPending ? PendingOverlayStatusLabel : string.Empty,
            effectiveColorKey,
            isPendingDelete);
    }

    private CalendarEventDisplayModel? TryMapPendingDraftToDisplayModel(PendingEvent pendingEvent)
    {
        if (pendingEvent.StartDatetime is null)
        {
            _logger.LogWarning(
                "Skipping pending draft {PendingEventId}: StartDatetime is null.",
                pendingEvent.PendingEventId);
            return null;
        }

        var startUtc = NormalizeUtc(pendingEvent.StartDatetime.Value);
        var endUtc = NormalizeUtc(pendingEvent.EndDatetime ?? pendingEvent.StartDatetime.Value);
        var isAllDay = pendingEvent.IsAllDay ?? false;
        var colorKey = _colorMappingService.NormalizeColorKey(pendingEvent.ColorId);

        DateTime startLocal, endLocal;
        if (isAllDay)
        {
            startLocal = startUtc.Date;
            endLocal = endUtc.Date;
        }
        else
        {
            startLocal = startUtc.ToLocalTime();
            endLocal = endUtc.ToLocalTime();
        }

        return new CalendarEventDisplayModel(
            pendingEvent.PendingEventId,
            CalendarEventSourceKind.Pending,
            pendingEvent.Summary ?? string.Empty,
            startUtc,
            endUtc,
            startLocal,
            endLocal,
            isAllDay,
            _colorMappingService.GetHexColor(colorKey),
            _colorMappingService.GetDisplayName(colorKey),
            false,
            pendingEvent.Description,
            null,
            true,
            false,
            0.6,
            pendingEvent.UpdatedAt,
            DraftStatusLabel,
            colorKey);
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

    private sealed record GoogleCalendarQueryRow(GcalEvent GcalEvent, PendingEvent? PendingEvent);
}
