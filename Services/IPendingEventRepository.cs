using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface IPendingEventRepository
{
    Task<PendingEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default);
    Task UpsertAsync(PendingEvent pendingEvent, CancellationToken ct = default);
}
