using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface IPendingEventRepository
{
    Task<PendingEvent?> GetByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default);
    Task<PendingEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default);
    Task<PendingEvent?> GetDayNameEventAsync(DateOnly date, CancellationToken ct = default);
    Task<PendingEvent?> GetSleepEventForDateAsync(DateOnly date, CancellationToken ct = default);
    Task UpsertAsync(PendingEvent pendingEvent, CancellationToken ct = default);
    Task DeleteByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default);
    Task DeleteByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default);
}
