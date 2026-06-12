using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface IEventRepository
{
    Task<Event?> GetByEventIdAsync(string eventId, CancellationToken ct = default);

    Task<Event?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default);

    Task<IList<Event>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<(DateOnly From, DateOnly To)?> GetStoredDateRangeAsync(CancellationToken ct = default);

    Task UpsertAsync(Event ev, CancellationToken ct = default);

    Task DeleteByEventIdAsync(string eventId, CancellationToken ct = default);

    Task<Event?> GetDayNameEventAsync(DateOnly date, CancellationToken ct = default);

    Task<Event?> GetSleepEventForDateAsync(DateOnly date, CancellationToken ct = default);
}
