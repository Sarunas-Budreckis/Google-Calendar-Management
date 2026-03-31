using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Services;

public sealed class CalendarQueryService : ICalendarQueryService
{
    private readonly IGcalEventRepository _gcalEventRepository;
    private readonly IColorMappingService _colorMappingService;

    public CalendarQueryService(
        IGcalEventRepository gcalEventRepository,
        IColorMappingService colorMappingService)
    {
        _gcalEventRepository = gcalEventRepository;
        _colorMappingService = colorMappingService;
    }

    public async Task<IList<CalendarEventDisplayModel>> GetEventsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var events = await _gcalEventRepository.GetByDateRangeAsync(from, to, ct);
        return events.Select(MapToDisplayModel).ToList();
    }

    public async Task<CalendarEventDisplayModel?> GetEventByGcalIdAsync(string gcalEventId, CancellationToken ct = default)
    {
        var gcalEvent = await _gcalEventRepository.GetByGcalEventIdAsync(gcalEventId, ct);
        return gcalEvent is null ? null : MapToDisplayModel(gcalEvent);
    }

    private CalendarEventDisplayModel MapToDisplayModel(GcalEvent gcalEvent)
    {
        var startUtc = DateTime.SpecifyKind(gcalEvent.StartDatetime ?? DateTime.UtcNow, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(gcalEvent.EndDatetime ?? startUtc, DateTimeKind.Utc);
        var startLocal = startUtc.ToLocalTime();
        var endLocal = endUtc.ToLocalTime();

        return new CalendarEventDisplayModel(
            gcalEvent.GcalEventId,
            gcalEvent.Summary ?? string.Empty,
            startUtc,
            endUtc,
            startLocal,
            endLocal,
            gcalEvent.IsAllDay ?? false,
            _colorMappingService.GetHexColor(gcalEvent.ColorId),
            gcalEvent.IsRecurringInstance,
            gcalEvent.Description,
            gcalEvent.LastSyncedAt);
    }
}
