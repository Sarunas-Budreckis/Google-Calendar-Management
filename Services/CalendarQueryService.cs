using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Models;
using Microsoft.Extensions.Logging;

namespace GoogleCalendarManagement.Services;

public sealed class CalendarQueryService : ICalendarQueryService
{
    private readonly IGcalEventRepository _gcalEventRepository;
    private readonly IColorMappingService _colorMappingService;
    private readonly ILogger<CalendarQueryService> _logger;

    public CalendarQueryService(
        IGcalEventRepository gcalEventRepository,
        IColorMappingService colorMappingService,
        ILogger<CalendarQueryService>? logger = null)
    {
        _gcalEventRepository = gcalEventRepository;
        _colorMappingService = colorMappingService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CalendarQueryService>.Instance;
    }

    public async Task<IList<CalendarEventDisplayModel>> GetEventsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var events = await _gcalEventRepository.GetByDateRangeAsync(from, to, ct);
        var result = new List<CalendarEventDisplayModel>(events.Count);
        foreach (var e in events)
        {
            var model = TryMapToDisplayModel(e);
            if (model is not null)
            {
                result.Add(model);
            }
        }

        return result;
    }

    public async Task<CalendarEventDisplayModel?> GetEventByGcalIdAsync(string gcalEventId, CancellationToken ct = default)
    {
        var gcalEvent = await _gcalEventRepository.GetByGcalEventIdAsync(gcalEventId, ct);
        return gcalEvent is null ? null : TryMapToDisplayModel(gcalEvent);
    }

    private CalendarEventDisplayModel? TryMapToDisplayModel(GcalEvent gcalEvent)
    {
        if (gcalEvent.StartDatetime is null)
        {
            _logger.LogWarning(
                "Skipping event {GcalEventId}: StartDatetime is null.",
                gcalEvent.GcalEventId);
            return null;
        }

        var startUtc = DateTime.SpecifyKind(gcalEvent.StartDatetime.Value, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(gcalEvent.EndDatetime ?? gcalEvent.StartDatetime.Value, DateTimeKind.Utc);
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
            gcalEvent.Summary ?? string.Empty,
            startUtc,
            endUtc,
            startLocal,
            endLocal,
            isAllDay,
            _colorMappingService.GetHexColor(gcalEvent.ColorId),
            gcalEvent.IsRecurringInstance,
            gcalEvent.Description,
            gcalEvent.LastSyncedAt);
    }
}
