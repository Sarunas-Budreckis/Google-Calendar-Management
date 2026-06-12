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

        var eventRows = await context.Events
            .AsNoTracking()
            .Where(e => !e.IsDeleted &&
                        e.StartDatetime.HasValue &&
                        e.StartDatetime < rangeEndExclusiveUtc &&
                        (e.EndDatetime ?? e.StartDatetime.Value) >= rangeStartUtc)
            .OrderBy(e => e.StartDatetime)
            .ThenBy(e => e.Summary)
            .ToListAsync(ct);

        var outlookRows = await context.OutlookEvents
            .AsNoTracking()
            .Where(e => !e.IsSuppressed &&
                        e.StartDatetime < rangeEndExclusiveUtc &&
                        e.EndDatetime >= rangeStartUtc)
            .OrderBy(e => e.StartDatetime)
            .ThenBy(e => e.Subject)
            .ToListAsync(ct);

        var result = new List<CalendarEventDisplayModel>(eventRows.Count + outlookRows.Count);
        foreach (var ev in eventRows)
        {
            var model = TryMapEventToDisplayModel(ev);
            if (model is not null && OverlapsRange(model, from, to))
            {
                result.Add(model);
            }
        }

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

        if (eventId.StartsWith("outlook_", StringComparison.Ordinal))
        {
            var outlookId = eventId["outlook_".Length..];
            var outlookEvent = await context.OutlookEvents
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.OutlookEventId == outlookId, ct);
            return outlookEvent is null ? null : MapOutlookEventToDisplayModel(outlookEvent);
        }

        var ev = await context.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.EventId == eventId && !e.IsDeleted, ct);
        return ev is null ? null : TryMapEventToDisplayModel(ev);
    }

    private CalendarEventDisplayModel? TryMapEventToDisplayModel(Event ev)
    {
        if (ev.StartDatetime is null)
        {
            _logger.LogWarning(
                "Skipping event {EventId}: StartDatetime is null.",
                ev.EventId);
            return null;
        }

        var startUtc = NormalizeUtc(ev.StartDatetime.Value);
        var endUtc = NormalizeUtc(ev.EndDatetime ?? ev.StartDatetime.Value);
        var isAllDay = ev.IsAllDay ?? false;
        var colorKey = _colorMappingService.NormalizeColorKey(ev.ColorId);
        var isCandidate = ev.Lifecycle == "candidate";
        var isPending = ev.Publish == "local_only" || ev.HasUnpublishedChanges || isCandidate;
        var sourceKind = ev.Publish == "published" && !isCandidate
            ? CalendarEventSourceKind.Google
            : CalendarEventSourceKind.Pending;

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
            ev.EventId,
            sourceKind,
            ev.Summary ?? string.Empty,
            startUtc,
            endUtc,
            startLocal,
            endLocal,
            isAllDay,
            _colorMappingService.GetHexColor(colorKey),
            _colorMappingService.GetDisplayName(colorKey),
            ev.IsRecurringInstance,
            ev.Description,
            ev.LastSyncedAt,
            isPending,
            false,
            isCandidate ? 0.5 : isPending ? 0.6 : 1.0,
            isPending ? ev.UpdatedAt : null,
            BuildEventStatusLabel(ev),
            colorKey);
    }

    private static string BuildEventStatusLabel(Event ev)
    {
        if (ev.Publish == "local_only")
        {
            return DraftStatusLabel;
        }

        return ev.HasUnpublishedChanges ? PendingOverlayStatusLabel : string.Empty;
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
