using GoogleCalendarManagement.Data.Entities;

namespace GoogleCalendarManagement.Services;

public interface IGcalEventRepository
{
    Task<IList<GcalEvent>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<GcalEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default);
}
