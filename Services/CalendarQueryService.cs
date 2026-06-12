using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Models;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class CalendarQueryService : ICalendarQueryService
{
    private const string CandidateStatusLabel = "Candidate event";
    private const string DraftStatusLabel = "Not yet published to Google Calendar";
    private const string PendingOverlayStatusLabel = "Local changes, pending push to GCal";
    private const string PendingDeleteStatusLabel = "Pending delete — will be removed from Google Calendar when pushed";

    private readonly IEventRepository _eventRepository;
    private readonly IColorMappingService _colorMappingService;
    private readonly ILogger<CalendarQueryService> _logger;

    public CalendarQueryService(
        IEventRepository eventRepository,
        IColorMappingService colorMappingService,
        ILogger<CalendarQueryService>? logger = null)
    {
        _eventRepository = eventRepository;
        _colorMappingService = colorMappingService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CalendarQueryService>.Instance;
    }

    public async Task<IList<CalendarEventDisplayModel>> GetEventsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var eventRows = await _eventRepository.GetByDateRangeAsync(from, to, ct);
        var result = new List<CalendarEventDisplayModel>(eventRows.Count);
        foreach (var ev in eventRows)
        {
            var model = MapEventToDisplayModel(ev);
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

        var ev = await _eventRepository.GetByEventIdAsync(eventId, ct);
        return ev is null || (ev.IsDeleted && !ev.HasUnpublishedChanges) ? null : MapEventToDisplayModel(ev);
    }

    private CalendarEventDisplayModel? MapEventToDisplayModel(Event ev)
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
        var isPendingDelete = ev.IsDeleted && ev.HasUnpublishedChanges;
        var sourceKind = ev.Lifecycle == "candidate"
            ? CalendarEventSourceKind.Candidate
            : ev.Publish == "published"
                ? CalendarEventSourceKind.Google
                : CalendarEventSourceKind.Pending;
        var isPending = ev.Publish == "local_only" || ev.HasUnpublishedChanges;

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
            isPendingDelete,
            ev.Lifecycle == "candidate" || ev.Publish == "local_only" || ev.HasUnpublishedChanges ? 0.6 : 1.0,
            isPending ? ev.UpdatedAt : null,
            BuildEventStatusLabel(ev),
            colorKey);
    }

    private static string BuildEventStatusLabel(Event ev)
    {
        if (ev.Lifecycle == "candidate")
        {
            return CandidateStatusLabel;
        }

        if (ev.IsDeleted && ev.HasUnpublishedChanges)
        {
            return PendingDeleteStatusLabel;
        }

        if (ev.Publish == "local_only")
        {
            return DraftStatusLabel;
        }

        return ev.HasUnpublishedChanges ? PendingOverlayStatusLabel : string.Empty;
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

}
