using GoogleCalendarManagement.Models;

namespace GoogleCalendarManagement.Services;

public interface ICalendarQueryService
{
    Task<IList<CalendarEventDisplayModel>> GetEventsForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<CalendarEventDisplayModel?> GetEventByIdAsync(string eventId, CancellationToken ct = default);
}
