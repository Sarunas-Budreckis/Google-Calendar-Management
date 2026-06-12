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
    private const string OutlookPurpleHex = "#8B5CF6";
    private const string OutlookColorName = "Purple (Work)";

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
        var rangeStartUtc = ToLocalDayBoundaryUtc(from);
        var rangeEndExclusiveUtc = ToLocalDayBoundaryUtc(to.AddDays(1));

        await using var context = await _dbContextFactory.CreateDbContextAsync(ct);

        // TODO 8.3+: rewrite the curated-event query against the unified `event` table.
        // The gcal_event / pending_event tables were merged into `event` in Story 8.2; the full
        // read path (lifecycle, has_unpublished_changes, candidate translucency) is rebuilt in
        // Stories 8.3 (repository) and 8.5 (rendering). Outlook rows are unaffected and kept.
        var outlookRows = await context.OutlookEvents
            .AsNoTracking()
            .Where(e => !e.IsSuppressed &&
                        e.StartDatetime < rangeEndExclusiveUtc &&
                        e.EndDatetime >= rangeStartUtc)
            .OrderBy(e => e.StartDatetime)
            .ThenBy(e => e.Subject)
            .ToListAsync(ct);

        var result = new List<CalendarEventDisplayModel>(outlookRows.Count);
        foreach (var outlookEvent in outlookRows)
        {
            var model = MapOutlookEventToDisplayModel(outlookEvent);
            if (OverlapsRange(model, from, to))
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

        // TODO 8.3+: resolve curated events from the unified `event` table (Stories 8.3/8.5).
        if (eventId.StartsWith("pending_", StringComparison.Ordinal))
        {
            return null;
        }

        if (eventId.StartsWith("outlook_", StringComparison.Ordinal))
        {
            var outlookId = eventId["outlook_".Length..];
            var outlookEvent = await context.OutlookEvents
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.OutlookEventId == outlookId, ct);
            return outlookEvent is null ? null : MapOutlookEventToDisplayModel(outlookEvent);
        }

        // TODO 8.3+: resolve curated events from the unified `event` table (Stories 8.3/8.5).
        return null;
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

    private static DateTime ToLocalDayBoundaryUtc(DateOnly date)
    {
        return date
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Local)
            .ToUniversalTime();
    }

    private static CalendarEventDisplayModel MapOutlookEventToDisplayModel(OutlookEvent e)
    {
        var startUtc = DateTime.SpecifyKind(e.StartDatetime, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(e.EndDatetime, DateTimeKind.Utc);

        DateTime startLocal, endLocal;
        if (e.IsAllDay)
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
            "outlook_" + e.OutlookEventId,
            CalendarEventSourceKind.Outlook,
            e.Subject,
            startUtc,
            endUtc,
            startLocal,
            endLocal,
            e.IsAllDay,
            OutlookPurpleHex,
            OutlookColorName,
            e.IsRecurring,
            e.BodyPreview,
            e.LastSyncedAt);
    }

    private sealed record GoogleCalendarQueryRow(GcalEvent GcalEvent, PendingEvent? PendingEvent);
}
